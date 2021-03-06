using game.Entities;
using game.GameScreens;
using game.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoyT.AStar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiledSharp;

namespace game.World
{
    public class Map
    {
        private class ScoutsOrder
        {
            public IScout Scout;
            public Position from;
            public Position to;
        }

        public TmxMap Data { get; private set; }
        public Dictionary<TmxTileset, Texture2D> Textures { get; private set; }

        private Queue<ScoutsOrder> pathQueue;
        private Grid pathFindingGrid;
        private ScreenManager screenManager;

        private Map(TmxMap data, Dictionary<TmxTileset, Texture2D> textures)
        {
            Data = data;
            Textures = textures;
            pathQueue = new Queue<ScoutsOrder>();

            BuildPathFindingGrid();
        }

        public void Update(GameTime gameTime)
        {
            //Handle pathfinding queue
            if (pathQueue.Count == 0)
                return;

            var orderHandleCount = pathQueue.Count == 1 ? 1 : pathQueue.Count / 32 + 1;
            //Console.WriteLine($"Handling {orderHandleCount} orders");
            while (orderHandleCount > 0)
            {
                HandlePathRequest(pathQueue.Dequeue());
                --orderHandleCount;
            }
        }

        private void HandlePathRequest(ScoutsOrder order)
        {
            var path = GetPath(order.from, order.to);
            var pathQueue = new Queue<Vector2>();
            if (path != null && path.Count > 1)
            {
                for (int i = 0; i < path.Count - 1; ++i)
                    pathQueue.Enqueue(path[i + 1]);
            }

            order.Scout.RecievePath(pathQueue);
        }

        private void BuildPathFindingGrid()
        {
            var collisionLayer = Data.Layers.FirstOrDefault(l => l.Name == "collision");
            if (collisionLayer == default(TmxLayer))
            {
                Console.WriteLine("Map loaded without collision layer.");
                return;
            }

            pathFindingGrid = new Grid(Data.Width, Data.Height);
            foreach (var tile in collisionLayer.Tiles)
            {
                if (tile.Gid != 0)
                    pathFindingGrid.BlockCell(new Position(tile.X, tile.Y));
            }
        }

        public void RequestPath(IScout scout, Position from, Position to)
        {
            pathQueue.Enqueue(new ScoutsOrder() {Scout = scout, from = from, to = to});
        }

        private List<Vector2> GetPath(Position from, Position to)
        {
            var result = new List<Vector2>();
            if (pathFindingGrid == null)
                return null;

            var path = pathFindingGrid.GetPath(from, to);
            foreach (var node in path)
                result.Add(new Vector2(node.X * 32 + 16, node.Y * 32 + 16));

            if (path.Count() == 0)
                return null;

            return result;
        }

        public Func<RayCaster.HitData, bool> GetIsTileOccupiedFunction(string layerName)
        {
            if (!Data.Layers.Contains(layerName))
                throw new ArgumentException($"{layerName} does not exist in this map");

            return hitData =>
            {
                Vector2 coordinates = hitData.tileCoordinates;
                if (coordinates.X < 0 || coordinates.X >= Data.Width ||
                    coordinates.Y < 0 || coordinates.Y >= Data.Height)
                    return false;

                int index = (int) (coordinates.Y * Data.Width + coordinates.X);
                TmxLayer wallLayer = Data.Layers[layerName];
                TmxLayerTile tile = wallLayer.Tiles[index];

                // if tileset is found it is solid
                return GetTilesetForTile(tile) != null;
            };
        }

        public static Map LoadTiledMap(GraphicsDevice graphicsDevice, string pathToMap)
        {
            TmxMap data = new TmxMap(pathToMap);
            Dictionary<TmxTileset, Texture2D> tilesetTextureMap = new Dictionary<TmxTileset, Texture2D>();

            string tilesheetFolder = @"Content/Tilesets";
            foreach (TmxTileset tileset in data.Tilesets)
            {
                string pathToResource = Path.Combine(tilesheetFolder, Path.GetFileName(tileset.Image.Source));
                if (!File.Exists(pathToResource))
                    continue;

                using (FileStream stream = new FileStream(pathToResource, FileMode.Open))
                {
                    Texture2D texture = Texture2D.FromStream(graphicsDevice, stream);
                    tilesetTextureMap.Add(tileset, texture);
                }
            }

            return new Map(data, tilesetTextureMap);
        }

        public TmxTileset GetTilesetForTile(TmxLayerTile tile)
        {
            // TODO: fix tileset choosing, might have to refactor the tiledSharp lib for it
            // we now assume that tilesets are ordered in ascending order by gid
            int tilesetCount = Data.Tilesets.Count;
            for (int i = 0; i < tilesetCount; i++)
            {
                TmxTileset tileset = Data.Tilesets[tilesetCount - 1 - i];
                if (tile.Gid >= tileset.FirstGid)
                    return tileset;
            }

            return null;
        }

        public Rectangle GetSourceRectangleForTile(TmxTileset tileset, TmxLayerTile tile)
        {
            Rectangle source = new Rectangle();
            int tileWidth = tileset.TileWidth;
            int tileHeight = tileset.TileHeight;
            int tilesInHorizontalAxis = tileset.Image.Width.GetValueOrDefault() / tileWidth;

            // depending on the tile gid get the correct tile coordinates
            int tileIndex = tile.Gid - tileset.FirstGid;
            int xTilePos = tileIndex / tilesInHorizontalAxis;
            int yTilePos = tileIndex - xTilePos * tilesInHorizontalAxis;

            source.Width = tileWidth;
            source.Height = tileHeight;

            source.X = yTilePos * tileWidth;
            source.Y = xTilePos * tileHeight;

            return source;
        }

        public Vector2 Move(Vector2 velocity, Entity entity, string collisionLayer = "collision")
        {
            TmxLayer layer = Data.Layers[collisionLayer];

            Vector2 newPosition = entity.position + velocity;
            Rectangle sweptBounds = new Rectangle((int) (newPosition.X - entity.Width / 2),
                (int) (newPosition.Y - entity.Height / 2),
                entity.Width, entity.Height);

            // create swept rectangle
            sweptBounds = Rectangle.Union(sweptBounds, entity.BoundingBox);

            int minTileX = sweptBounds.Left / Data.TileWidth;
            int minTileY = sweptBounds.Top / Data.TileHeight;

            int maxTileX = sweptBounds.Right / Data.TileWidth + 1;
            int maxTileY = sweptBounds.Bottom / Data.TileHeight + 1;

            for (int y = minTileY; y < maxTileY; y++)
            {
                for (int x = minTileX; x < maxTileX; x++)
                {
                    if (x < 0 || x >= Data.Width ||
                        y < 0 || y >= Data.Height)
                        continue;

                    TmxLayerTile tile = layer.Tiles[y * Data.Width + x];
                    if (GetTilesetForTile(tile) == null)
                        continue;

                    Rectangle tileBounds = GetTileBounds(x, y);
                    Rectangle intersection = Rectangle.Intersect(tileBounds, sweptBounds);

                    if (intersection.Width < intersection.Height)
                        velocity.X += -Math.Sign(velocity.X) * intersection.Width;
                    else
                        velocity.Y += -Math.Sign(velocity.Y) * intersection.Height;
                }
            }

            return velocity;
        }

        private Rectangle GetTileBounds(int x, int y)
        {
            return new Rectangle(x * Data.TileWidth, y * Data.TileHeight, Data.TileWidth, Data.TileHeight);
        }

        public void LoadObjects(ScreenManager screenManager)
        {
            this.screenManager = screenManager;

            foreach (var obj in Data.ObjectGroups[0].Objects)
            {
                var tile = obj.Tile;
                TmxTileset tileset = GetTilesetForTile(tile);

                if (tileset == null)
                    continue;

                Texture2D tilesetTexture = Textures[tileset];
                var source = GetSourceRectangleForTile(tileset, obj.Tile);

                CreateObject(obj, source, tileset, tilesetTexture);
            }
        }

        private void CreateObject(TmxObject obj, Rectangle source, TmxTileset tileset, Texture2D tilesetTexture)
        {
            var random = new Random();
            var type = tileset.Tiles[obj.Tile.Gid - tileset.FirstGid].Type;
            var rotation = MathHelper.ToRadians((float)obj.Rotation);
            var spawnPosition = GetObjectPosition(obj, source);
            Entity entity;

            switch (type)
            {
                case "Car":
                    entity = new Car(this, tilesetTexture, (int) obj.Width, (int) obj.Height, spawnPosition,
                        rotation, source);
                    break;
                case "Enemy_Spawn":
                    entity = new Enemy(tilesetTexture, spawnPosition, (int) obj.Width, (int) obj.Height, this,
                        rotation, source);
                    break;
                case "Dungeon_Entrance":
                    entity = new DungeonEntrance(new ShooterScreen(), screenManager, tilesetTexture, (int) obj.Width,
                        (int) obj.Height, spawnPosition, rotation, source);
                    break;
                case "Collectable":
                    entity = new Collectable(tilesetTexture, spawnPosition, (int)obj.Width, (int)obj.Height, rotation, source);
                    break;
                case "Ammo":
                    var randomBulletType = (BulletType) random.Next(Enum.GetNames(typeof(BulletType)).Length);
                    entity = new AmmoPack(randomBulletType, random.Next(15, 30), tilesetTexture, spawnPosition,
                        (int) obj.Width, (int) obj.Height, rotation, source);
                    break;
                case "Health":
                    entity = new HealthPack(random.Next(15, 30), tilesetTexture, spawnPosition, (int) obj.Width,
                        (int) obj.Height, rotation, source);
                    break;
                case "Gas":
                    entity = new GasPump(tilesetTexture, (int)obj.Width, (int)obj.Height, spawnPosition, rotation, source);
                    break;
                default:
                    entity = new Entity(tilesetTexture, (int) obj.Width, (int) obj.Height, spawnPosition, rotation,
                        source);
                    break;
            }

            EntityManager.Instance.AddEntity(entity);
        }

        private static Vector2 GetObjectPosition(TmxObject obj, Rectangle source)
        {
            var scale = new Vector2((float) (obj.Width / source.Width), (float) (obj.Height / source.Height));
            var position = new Vector2((float) (obj.X + (source.Width * scale.X / 2)),
                (float) (obj.Y - (source.Height * scale.Y / 2)));

            if (obj.Rotation == 90 || obj.Rotation == -270)
            {
                position.Y += (int) obj.Height;
            }

            else if (obj.Rotation == 180 || obj.Rotation == -180)
            {
                position.Y += (int) obj.Height;
                position.X -= (int) obj.Width;
            }

            else if (obj.Rotation == 270 || obj.Rotation == -90)
            {
                position.X -= (int) obj.Width;
            }

            return position;
        }
    }
}
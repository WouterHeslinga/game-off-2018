using System.Collections.Generic;
using Comora;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TiledSharp;

namespace game
{
    public class TiledMapRenderer
    {
        public void Render(Map map, SpriteBatch batch, Camera camera)
        {
            foreach (TmxLayer layer in map.Data.Layers)
            {
                if (!layer.Visible)
                    continue;

                List<TmxLayerTile> visibleTiles = GetVisibleTilesForLayer(map, layer, camera);
                foreach (TmxLayerTile tile in visibleTiles)
                {
                    Rectangle source, destination;
                    TmxTileset tileset = map.GetTilesetForTile(tile);

                    if (tileset == null)
                        continue;

                    Texture2D tilesetTexture = map.Textures[tileset];

                    map.GetSourceAndDestinationRectangles(tileset, tile, out source, out destination);
                    batch.Draw(tilesetTexture, destination, source, Color.White, 0, Vector2.Zero, SpriteEffects.None,
                        0);
                }
            }
        }

        public List<TmxLayerTile> GetVisibleTilesForLayer(Map map, TmxLayer layer, Camera camera)
        {
            List<TmxLayerTile> indexList = new List<TmxLayerTile>();
            TmxMap data = map.Data;
            Rectangle cameraBounds = camera.GetBounds();

            // calculate how many tiles to draw
            int cameraTilesWidth = ((int) camera.Width / data.TileWidth) + 2;
            int cameraTilesHeight = ((int) camera.Height / data.TileHeight) + 2;

            // get camera position in tiles
            int xCameraStartTile = (int) ((camera.Position.X - cameraBounds.Width / 2) / data.TileWidth);
            int yCameraStartTile = (int) ((camera.Position.Y - cameraBounds.Height / 2) / data.TileHeight);

            int xCameraEndTile = xCameraStartTile + cameraTilesWidth;
            int yCameraEndTile = yCameraStartTile + cameraTilesHeight;

            // clamp values
            ClampValue(ref xCameraStartTile, data.Width);
            ClampValue(ref xCameraEndTile, data.Width);
            ClampValue(ref yCameraStartTile, data.Height);
            ClampValue(ref yCameraEndTile, data.Height);

            for (int y = yCameraStartTile; y < yCameraEndTile; y++)
            {
                for (int x = xCameraStartTile; x < xCameraEndTile; x++)
                {
                    TmxLayerTile tile = layer.Tiles[y * data.Width + x];
                    indexList.Add(tile);
                }
            }

            return indexList;
        }

        private void ClampValue(ref int value, int maxValue)
        {
            if (value < 0)
                value = 0;
            else if (value > maxValue)
                value = maxValue;
        }

    }
}
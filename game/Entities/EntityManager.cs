﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace game.Entities
{
    static class EntityManager
    {
        private static List<Entity> entities;

        public static void Initialize()
        {
            entities = new List<Entity>();
        }

        public static void Update(GameTime gameTime)
        {
            foreach (var entity in entities.ToArray())
            {
                entity.Update(gameTime);
            }

            PostUpdate();
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            foreach (var entity in entities)
            {
                entity.Draw(spriteBatch);
            }
        }

        public static void AddEntity(Entity entity)
        {
            entities.Add(entity);
        }

        /// <summary>
        /// Check if any entity has to be removed from the game
        /// </summary>
        private static void PostUpdate()
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].ShouldBeDestroyed)
                {
                    entities.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}

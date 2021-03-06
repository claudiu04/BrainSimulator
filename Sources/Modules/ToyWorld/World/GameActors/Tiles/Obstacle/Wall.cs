﻿using System;
using VRageMath;
using World.Atlas;
using World.Atlas.Layers;
using World.GameActions;

namespace World.GameActors.Tiles.Obstacle
{
    /// <summary>
    ///     Wall can be transformed to DamagedWall if pickaxe is used
    /// </summary>
    public class Wall : StaticTile, IInteractableGameActor
    {
        public Wall() : base() { } 

 		public Wall(int textureId) : base(textureId) { }

        public Wall(string textureName)
            : base(textureName)
        {
        }

        public void ApplyGameAction(IAtlas atlas, GameAction gameAction, Vector2 position)
        {
            if (!(gameAction is UsePickaxe))
                return;

            UsePickaxe usePickaxe = (UsePickaxe)gameAction;
            if (Math.Abs(usePickaxe.Damage) < 0.00001f)
                return;

            if (usePickaxe.Damage >= 1.0f)
            {
                atlas.ReplaceWith(new GameActorPosition(this, position, LayerType.ObstacleInteractable), new DestroyedWall());
                return;
            }
            atlas.ReplaceWith(new GameActorPosition(this, position, LayerType.ObstacleInteractable), new DamagedWall(usePickaxe.Damage, Vector2I.Zero));
        }
    }

    /// <summary>
    ///     DamagedWall has health from (0,1) excl. If health leq 0, it is replaced by DestroyedWall.
    ///     Only way how to make damage is to use pickaxe.
    /// </summary>
    public class DamagedWall : DynamicTile, IInteractableGameActor
    {
        public float Health { get; private set; }

        private DamagedWall(Vector2I position) : base(position)
        {
            Health = 1f;
        }

        public DamagedWall(float damage, Vector2I position)
            : this(position)
        {
            Health -= damage;
        }

        public void ApplyGameAction(IAtlas atlas, GameAction gameAction, Vector2 position)
        {
            UsePickaxe action = gameAction as UsePickaxe;
            if (action != null)
            {
                UsePickaxe usePickaxe = action;
                Health -= usePickaxe.Damage;
            }

            if (Health <= 0f)
                atlas.ReplaceWith(new GameActorPosition(this, position, LayerType.ObstacleInteractable), new DestroyedWall());
        }
    }

    /// <summary>
    /// </summary>
    public class DestroyedWall : StaticTile
    {
        public DestroyedWall() : base (){ } 

 		public DestroyedWall(int textureId) : base(textureId) { }
    }
}
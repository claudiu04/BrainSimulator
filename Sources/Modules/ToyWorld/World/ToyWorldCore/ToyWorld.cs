﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;
using TmxMapSerializer.Elements;
using VRageMath;
using World.Atlas;
using World.GameActors;
using World.GameActors.GameObjects;
using World.GameActors.Tiles;
using World.Lua;
using World.Physics;
using World.WorldInterfaces;

namespace World.ToyWorldCore
{
    public class ToyWorld : IWorld, IDisposable
    {
        private LuaConsole m_luaConsole;

        private ICollisionResolver m_collisionResolver;
        private readonly Thread m_consoleThread;

        public Vector2I Size { get; private set; }
        public AutoupdateRegister AutoupdateRegister { get; protected set; }
        public TileDetectorRegister TileDetectorRegister { get; protected set; }
        public IAtlas Atlas { get; protected set; }
        public TilesetTable TilesetTable { get; protected set; }
        public IPhysics Physics { get; protected set; }
        public bool LuaThoroughSync { get; set; }

        public bool IsWinterEnabled
        {
            get { return Atlas.IsWinterEnabled; }
            set { Atlas.IsWinterEnabled = value; }
        }

        public delegate void ToyWorldDisposedHandler(object sender);
        public event ToyWorldDisposedHandler ToyWorldDisposed;

        /// <summary>
        /// Dictionary with signal names as keys. This prevents user from defining two signals with the same name.
        /// Keys are delegates which takes IAtlas and return float -- value of the signal.
        /// To obtain values of signals, just iterate the dictionary and call the items with IAtlas parameter
        /// </summary>
        public Dictionary<string, Func<IAtlas, float>> SignalDispatchers { get; protected set; }

        public ToyWorld(Map tmxDeserializedMap, StreamReader tileTable)
        {
            if (tileTable == null)
                throw new ArgumentNullException("tileTable");
            if (tmxDeserializedMap == null)
                throw new ArgumentNullException("tmxDeserializedMap");
            Contract.EndContractBlock();

            Size = new Vector2I(tmxDeserializedMap.Width, tmxDeserializedMap.Height);
            AutoupdateRegister = new AutoupdateRegister(100000);
            TilesetTable = new TilesetTable(tmxDeserializedMap, tileTable);

            InitAtlas(tmxDeserializedMap);
            InitPhysics();
            TileDetectorRegister = new TileDetectorRegister(Atlas);
            RegisterSignals();

            m_consoleThread = new Thread(() =>
            {
                m_luaConsole = new LuaConsole(this, Atlas);
                m_luaConsole.ShowDialog();
            });
            m_consoleThread.Start();
        }

        private void RegisterSignals()
        {
            Func<IAtlas, float> inventoryItem = x =>
            {
                IPickableGameActor tool = x.GetAvatars().First().Tool;
                return tool != null ? tool.TilesetId : 0;
            };

            Func<IAtlas, float> avatarEnergy = x =>
            {
                IAvatar avatar = x.GetAvatars().FirstOrDefault();
                return avatar != null ? avatar.Energy : -1;
            };

            Func<IAtlas, float> avatarRested = x =>
            {
                IAvatar avatar = x.GetAvatars().FirstOrDefault();
                return avatar != null ? avatar.Rested : -1;
            };

            Func<IAtlas, float> puppetControl = x =>
            {
                IAvatar avatar = x.GetAvatars().FirstOrDefault();
                return avatar != null ? avatar.PuppetControlled ? 1 : 0 : -1;
            };

            Func<IAtlas, float> avatarPositionX = x =>
            {
                IAvatar avatar = x.GetAvatars().FirstOrDefault();
                return avatar != null ? avatar.Position.X : 0f;
            };

            Func<IAtlas, float> avatarPositionY = x =>
            {
                IAvatar avatar = x.GetAvatars().FirstOrDefault();
                return avatar != null ? avatar.Position.Y : 0f;
            };

            Func<IAtlas, float> avatarRotation = x =>
            {
                IAvatar avatar = x.GetAvatars().FirstOrDefault();
                // in radians, zero = avatar is lookning on the top
                return avatar != null ? avatar.Rotation % (2 * (float)Math.PI) : 0f;
            };

            Func<IAtlas, float> avatarReward = x =>
            {
                IAvatar avatar = x.GetAvatars().FirstOrDefault();
                return avatar != null ? avatar.Reward : 0;
            };

            SignalDispatchers = new Dictionary<string, Func<IAtlas, float>>();

            SignalDispatchers.Add("Item", inventoryItem);
            SignalDispatchers.Add("Energy", avatarEnergy);
            SignalDispatchers.Add("Rested", avatarRested);
            SignalDispatchers.Add("Puppet", puppetControl);
            SignalDispatchers.Add("AbsPosX", avatarPositionX);
            SignalDispatchers.Add("AbsPosY", avatarPositionY);
            SignalDispatchers.Add("Rotation", avatarRotation);
            SignalDispatchers.Add("Reward", avatarReward);
            // if you add a new signal, change the SignalCount variable accordingly

            Debug.Assert(TWConfig.Instance.SignalCount == SignalDispatchers.Count, "Number of signals has to be defined in SignalCount!");
        }

        private void InitAtlas(Map tmxDeserializedMap)
        {
            Action<GameActor> initializer = delegate (GameActor actor)
            {
                IAutoupdateable updateable = actor as IAutoupdateable;
                if (updateable != null && updateable.NextUpdateAfter > 0)
                    AutoupdateRegister.Register(updateable, updateable.NextUpdateAfter);
            };
            Atlas = MapLoader.LoadMap(tmxDeserializedMap, TilesetTable, initializer);

            Atlas.DayLength = TWConfig.Instance.DayLengh;
            Atlas.YearLength = TWConfig.Instance.YearLength;

            IAtmosphere atmosphere = new SimpleAtmosphere(Atlas);
            Atlas.Atmosphere = atmosphere;
        }

        private void InitPhysics()
        {
            Physics = new Physics.Physics();

            IMovementPhysics movementPhysics = new MovementPhysics();
            ICollisionChecker collisionChecker = new CollisionChecker(Atlas);

            // TODO MICHAL: setter for physics implementation
            /*
            m_collisionResolver = new NaiveCollisionResolver(collisionChecker, movementPhysics);
            /*/
            m_collisionResolver = new MomentumCollisionResolver(collisionChecker, movementPhysics);
            //*/

            //Log.Instance.Debug("World.ToyWorldCore.ToyWorld: Loading Successful");
        }


        //
        // TODO: methods below will be moved to some physics class
        //


        #region Updating

        public void Update()
        {
            if (LuaThoroughSync)
            {
                m_luaConsole.NotifyAndWait();
            }
            else
            {
                m_luaConsole.Notify();
            }

            UpdateTime();
            UpdateScheduled();
            UpdateLayers();
            UpdateAvatars();
            UpdateCharacters();
            UpdatePhysics();
            UpdateAtmosphere();

            //Log.Instance.Debug("World.ToyWorldCore.ToyWorld: ===================Step performed======================");
        }

        private void UpdateTime()
        {
            Atlas.IncrementTime(TWConfig.Instance.StepLength);
            //Log.Instance.Debug("ToyWorld time is: " + Atlas.RealTime);
        }

        private void UpdateScheduled()
        {
            TileDetectorRegister.Update();
            AutoupdateRegister.UpdateItems(Atlas);
            AutoupdateRegister.Tick();
        }

        private void UpdateLayers()
        {
            Atlas.UpdateLayers();
        }

        private void UpdateCharacters()
        {
            List<ICharacter> characters = Atlas.Characters;
            List<IForwardMovablePhysicalEntity> forwardMovablePhysicalEntities = characters.Select(x => x.PhysicalEntity).ToList();
            Physics.MoveMovableDirectable(forwardMovablePhysicalEntities);
        }

        private void UpdateAvatars()
        {
            List<IAvatar> avatars = Atlas.GetAvatars();
            Physics.TransformControlsPhysicalProperties(avatars);
            List<IForwardMovablePhysicalEntity> forwardMovablePhysicalEntities = avatars.Select(x => x.PhysicalEntity).ToList();
            Physics.MoveMovableDirectable(forwardMovablePhysicalEntities);
        }

        private void UpdatePhysics()
        {
            m_collisionResolver.ResolveCollisions();
        }

        private void UpdateAtmosphere()
        {
            Atlas.Atmosphere.Update();
        }

        #endregion

        public void RunLuaScript(string scriptPath)
        {
            m_luaConsole.RunScript(scriptPath);
        }

        public List<int> GetAvatarsIds()
        {
            return Atlas.Avatars.Keys.ToList();
        }

        public List<string> GetAvatarsNames()
        {
            return Atlas.Avatars.Values.Select(x => x.Name).ToList();
        }

        public IAvatar GetAvatar(int id)
        {
            return Atlas.Avatars[id];
        }

        [ContractInvariantMethod]
        private void Invariants()
        {
            Contract.Invariant(Atlas != null, "Atlas cannot be null");
            Contract.Invariant(AutoupdateRegister != null, "Autoupdate register cannot be null");
        }

        public void Dispose()
        {
            ToyWorldDisposed?.Invoke(this);
        }
    }
}
﻿using System;
using System.Linq;
using Exiled.API.Features;
using MapGeneration;
using slocLoader.AutoObjectLoader;

namespace slocLoader {

    public class slocPlugin : Plugin<Config> {

        public override string Name => "slocLoader";
        public override string Prefix => "sloc";
        public override string Author => "Axwabo";
        public override Version Version { get; } = new(1, 1, 0);
        public override Version RequiredExiledVersion { get; } = new(5, 2, 0);

        public override void OnEnabled() {
            base.OnEnabled();
            API.UnsetPrefabs();
            if (Config.AutoLoad)
                API.PrefabsLoaded += AutomaticObjectLoader.LoadObjects;
            if (SeedSynchronizer.MapGenerated) {
                API.LoadPrefabs();
                SpawnDefault();
            }

            Exiled.Events.Handlers.Map.Generated += API.LoadPrefabs;
            API.PrefabsLoaded += SpawnDefault;
        }

        public override void OnDisabled() {
            base.OnDisabled();
            API.UnsetPrefabs();
            Exiled.Events.Handlers.Map.Generated -= API.LoadPrefabs;
            API.PrefabsLoaded -= AutomaticObjectLoader.LoadObjects;
            API.PrefabsLoaded -= SpawnDefault;
        }

        private void SpawnDefault() {
            if (Config.EnableAutoSpawn)
                AutomaticObjectLoader.SpawnObjects(Config.AutoSpawnByRoomName.Cast<IAssetLocation>().Concat(Config.AutoSpawnByRoomType.Cast<IAssetLocation>()).Concat(Config.AutoSpawnByLocation.Cast<IAssetLocation>()));
        }

    }

}

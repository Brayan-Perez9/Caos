using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Caos.Dungeon;

namespace Caos.Tests
{
    public class DungeonRouteTests
    {
        [Test]
        public void Build_AlwaysStartsAtEntranceAndEndsAtBoss()
        {
            for (int seed = 0; seed < 12; seed++)
            {
                List<DungeonRoomKind> route = DungeonRoute.Build(seed);
                Assert.That(route[0], Is.EqualTo(DungeonRoomKind.Entrada));
                Assert.That(route[route.Count - 1], Is.EqualTo(DungeonRoomKind.Jefe));
                Assert.That(DungeonRoute.IsValid(route), Is.True);
            }
        }

        [Test]
        public void Build_SameSeedProducesSameRoute()
        {
            List<DungeonRoomKind> first = DungeonRoute.Build(20261127);
            List<DungeonRoomKind> second = DungeonRoute.Build(20261127);
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Build_ExtraRoomsKeepAllRequiredRooms()
        {
            List<DungeonRoomKind> route = DungeonRoute.Build(7, 3);
            Assert.That(DungeonRoute.IsValid(route), Is.True);
            Assert.That(route.Count, Is.GreaterThan(DungeonRoute.Required.Length));
            Assert.That(route[route.Count - 1], Is.EqualTo(DungeonRoomKind.Jefe));
        }

        [Test]
        public void Config_DefaultsIncludeEveryRoomAndRelic()
        {
            DungeonConfig config = ScriptableObject.CreateInstance<DungeonConfig>();
            try
            {
                config.ApplyDefaults();
                Assert.That(config.rooms, Is.Not.Null);
                Assert.That(config.relics, Is.Not.Null);

                for (int i = 0; i < DungeonRoute.Required.Length; i++)
                    Assert.That(config.Find(DungeonRoute.Required[i]), Is.Not.Null,
                        "Falta la sala " + DungeonRoute.Required[i]);

                Assert.That(config.relics.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(config.roomRadius, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}

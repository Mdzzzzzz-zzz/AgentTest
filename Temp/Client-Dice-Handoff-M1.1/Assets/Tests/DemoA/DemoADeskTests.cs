using DiceDemo.DemoA;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiceDemo.DemoA.Tests
{
    public sealed class DemoADeskTests
    {
        private const string ScenePath = "Assets/Scenes/DeskDemoA.unity";

        [SetUp]
        public void OpenDemoScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [Test]
        public void DeskScene_UsesOriginalCameraBaseline()
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera);
            Assert.IsTrue(camera.orthographic);
            Assert.AreEqual(DemoADeskRuntime.OriginalCameraSize, camera.orthographicSize, 0.001f);
        }

        [Test]
        public void DeskScene_ContainsOriginalBoundaryAndDropTriggers()
        {
            DemoADeskRuntime desk = Object.FindObjectOfType<DemoADeskRuntime>();
            Assert.IsNotNull(desk);
            Assert.GreaterOrEqual(desk.StaticBoundaryCount, 3);
            Assert.AreEqual(2, desk.DropTriggerCount);
        }

        [Test]
        public void DeskScene_ContainsOriginalSpawnPoints()
        {
            DemoADeskRuntime desk = Object.FindObjectOfType<DemoADeskRuntime>();
            Assert.IsNotNull(desk);
            Assert.AreEqual(85, desk.SpawnPointCount);
            Assert.IsNotNull(desk.SpawnPointRoot);
        }

        [Test]
        public void DeskScene_DoesNotContainLegacyTwoDimensionalPhysics()
        {
            Assert.AreEqual(0, Object.FindObjectsOfType<Rigidbody2D>().Length);
            Assert.AreEqual(1, Object.FindObjectsOfType<Camera>().Length);
        }
    }
}

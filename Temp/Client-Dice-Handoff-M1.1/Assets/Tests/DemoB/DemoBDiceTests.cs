using DiceDemo.DemoB;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiceDemo.DemoB.Tests
{
    public sealed class DemoBDiceTests
    {
        private const string ScenePath = "Assets/Scenes/DiceDemoB.unity";
        private DemoBDiceRuntime _runtime;

        [SetUp]
        public void OpenDemoScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
            _runtime = Object.FindObjectOfType<DemoBDiceRuntime>();
            Assert.IsNotNull(_runtime);
        }

        [Test]
        public void Scene_UsesOriginalThreeDimensionalBaseline()
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera);
            Assert.IsTrue(camera.orthographic);
            Assert.AreEqual(DemoBDiceRuntime.OriginalCameraSize, camera.orthographicSize, 0.001f);
            Assert.AreEqual(0, Object.FindObjectsOfType<Rigidbody2D>().Length);
            Assert.AreEqual(1, Object.FindObjectsOfType<Camera>().Length);
            Assert.AreEqual(1, Object.FindObjectsOfType<Rigidbody>().Length);
        }

        [Test]
        public void Dice_UsesOriginalBodyAndColliderParameters()
        {
            Rigidbody body = _runtime.DiceBody;
            BoxCollider collider = _runtime.DiceCollider;
            Assert.IsNotNull(body);
            Assert.IsNotNull(collider);
            Assert.AreEqual(10f, body.mass, 0.001f);
            Assert.AreEqual(0.005f, body.drag, 0.0001f);
            Assert.AreEqual(0.05f, body.angularDrag, 0.0001f);
            Assert.AreEqual(DemoBDiceRuntime.OriginalColliderSize, collider.size);
            Assert.IsNotNull(collider.sharedMaterial);
            Assert.AreEqual(0.05f, collider.sharedMaterial.dynamicFriction, 0.001f);
            Assert.AreEqual(0.05f, collider.sharedMaterial.staticFriction, 0.001f);
            Assert.AreEqual(0.55f, collider.sharedMaterial.bounciness, 0.001f);
            Assert.AreEqual(PhysicMaterialCombine.Maximum, collider.sharedMaterial.bounceCombine);
        }

        [Test]
        public void Dice_ContainsNineCompleteVisualVariants()
        {
            Assert.IsNotNull(_runtime.VisualVariants);
            Assert.AreEqual(9, _runtime.VisualVariants.Length);
            int activeCount = 0;
            foreach (Transform variant in _runtime.VisualVariants)
            {
                Assert.IsNotNull(variant);
                if (variant.gameObject.activeSelf) activeCount++;
                for (int face = 1; face <= 6; face++)
                    Assert.IsNotNull(FindChild(variant, "Side " + face), variant.name + " 缺少第 " + face + " 面");
            }
            Assert.AreEqual(1, activeCount);
            Assert.AreEqual(6, _runtime.GetActiveSides().Length);
        }

        [Test]
        public void Dice_ThrowCreatesRealAngularVelocity()
        {
            _runtime.ThrowDice();
            Assert.Greater(_runtime.DiceBody.angularVelocity.sqrMagnitude, 0.01f);
        }

        [Test]
        public void Dice_TopFaceRecognitionReturnsOneThroughSix()
        {
            _runtime.transform.rotation = Quaternion.identity;
            int face = _runtime.IdentifyTopFace();
            Assert.That(face, Is.InRange(1, 6));
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}

using DiceDemo.DemoC;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiceDemo.DemoC.Tests
{
    public sealed class DemoCDiceTests
    {
        private const string ScenePath = "Assets/Scenes/DiceDemoC.unity";
        private DemoCDiceRuntime _runtime;

        [SetUp]
        public void OpenDemoScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
            _runtime = Object.FindObjectOfType<DemoCDiceRuntime>();
            Assert.IsNotNull(_runtime);
            _runtime.ClearAllDice();
        }

        [Test]
        public void Scene_UsesOriginalThreeDimensionalDeskAndCameraBaseline()
        {
            Assert.IsNotNull(GameObject.Find("原作森林牌桌"));
            Assert.IsNotNull(Camera.main);
            Assert.IsTrue(Camera.main.orthographic);
            Assert.AreEqual(DemoCDiceRuntime.OriginalCameraSize, Camera.main.orthographicSize, 0.001f);
            Assert.AreEqual(0, Object.FindObjectsOfType<Rigidbody2D>().Length);
        }

        [Test]
        public void Template_PreservesOriginalPhysicsAndNineCompleteVisualLevels()
        {
            DemoCDie template = _runtime.DiceTemplate;
            Assert.IsNotNull(template);
            Assert.IsNotNull(template.Body);
            Assert.IsNotNull(template.Collider);
            Assert.AreEqual(DemoCDiceRuntime.OriginalMass, template.Body.mass, 0.001f);
            Assert.AreEqual(DemoCDiceRuntime.OriginalLinearDamping, template.Body.drag, 0.0001f);
            Assert.AreEqual(DemoCDiceRuntime.OriginalAngularDamping, template.Body.angularDrag, 0.0001f);
            Assert.AreEqual(DemoCDiceRuntime.OriginalColliderSize, template.Collider.size);
            Assert.IsNotNull(template.Collider.sharedMaterial);
            Assert.AreEqual(0.55f, template.Collider.sharedMaterial.bounciness, 0.001f);
            Assert.AreEqual(9, template.VisualVariants.Length);
            for (int level = 1; level <= 9; level++) Assert.AreEqual(6, template.CountFaces(level));
        }

        [Test]
        public void FaceNumbers_UseDepthTestMaterialSoHiddenFacesAreOccluded()
        {
            DemoCDie template = _runtime.DiceTemplate;
            TextMesh[] numbers = template.GetComponentsInChildren<TextMesh>(true);
            Assert.IsNotEmpty(numbers);
            foreach (TextMesh number in numbers)
            {
                MeshRenderer renderer = number.GetComponent<MeshRenderer>();
                Assert.IsNotNull(renderer);
                Assert.IsNotNull(renderer.sharedMaterial);
                Assert.AreEqual("DiceDemo/数字深度遮挡", renderer.sharedMaterial.shader.name);
            }
        }

        [Test]
        public void Arena_AllowsMultipleIndependentDiceUpToDeclaredCapacity()
        {
            for (int i = 0; i < DemoCDiceRuntime.MaximumDice; i++)
                Assert.IsNotNull(_runtime.SpawnPlacedDie(i % 9 + 1, new Vector3(i * 1.5f, 1f, 0f)));
            Assert.AreEqual(DemoCDiceRuntime.MaximumDice, _runtime.ActiveDiceCount);
            Assert.IsNull(_runtime.SpawnPlacedDie(1, Vector3.zero));
            foreach (DemoCDie die in _runtime.GetDiceSnapshot())
            {
                Assert.IsTrue(die.IsSettled);
                Assert.IsTrue(die.Body.IsSleeping());
                Assert.IsNotNull(die.Collider);
            }
        }

        [Test]
        public void Throw_CreatesRealLinearAndAngularMotionWithoutRemovingExistingDice()
        {
            _runtime.SpawnPlacedDie(2, new Vector3(-2f, 1f, 0f));
            _runtime.ThrowSelectedDie();
            Assert.AreEqual(2, _runtime.ActiveDiceCount);
            DemoCDie thrown = _runtime.GetDiceSnapshot()[1];
            Assert.Greater(thrown.Body.velocity.sqrMagnitude, 0.01f);
            Assert.Greater(thrown.Body.angularVelocity.sqrMagnitude, 0.01f);
        }

        [Test]
        public void MergeRule_RejectsDifferentLevelsAndMaximumLevel()
        {
            DemoCDie levelOne = _runtime.SpawnPlacedDie(1, new Vector3(-2f, 1f, 0f));
            DemoCDie levelTwo = _runtime.SpawnPlacedDie(2, new Vector3(0f, 1f, 0f));
            DemoCDie levelNineA = _runtime.SpawnPlacedDie(9, new Vector3(2f, 1f, 0f));
            DemoCDie levelNineB = _runtime.SpawnPlacedDie(9, new Vector3(4f, 1f, 0f));
            Assert.IsFalse(_runtime.CanMerge(levelOne, levelTwo));
            Assert.IsFalse(_runtime.TryMerge(levelOne, levelTwo));
            Assert.IsFalse(_runtime.CanMerge(levelNineA, levelNineB));
            Assert.AreEqual(4, _runtime.ActiveDiceCount);
            Assert.AreEqual(0, _runtime.MergeCount);
        }

        [Test]
        public void MergeRule_ConsumesTwoEqualDiceAndCreatesOneHigherLevelDie()
        {
            DemoCDie first = _runtime.SpawnPlacedDie(3, new Vector3(-0.7f, 1f, 0f));
            DemoCDie second = _runtime.SpawnPlacedDie(3, new Vector3(0.7f, 1f, 0f));
            Assert.IsTrue(_runtime.TryMerge(first, second));
            Assert.AreEqual(1, _runtime.MergeCount);
            Assert.AreEqual(1, _runtime.ActiveDiceCount);
            Assert.AreEqual(4, _runtime.GetDiceSnapshot()[0].Level);
        }

        [Test]
        public void MergeDemonstration_StartsWithTwoEqualMovingPhysicalDice()
        {
            _runtime.StartMergeDemonstration();
            DemoCDie[] dice = _runtime.GetDiceSnapshot();
            Assert.AreEqual(2, dice.Length);
            Assert.AreEqual(1, dice[0].Level);
            Assert.AreEqual(1, dice[1].Level);
            Assert.Greater(dice[0].Body.velocity.sqrMagnitude, 0.01f);
            Assert.Greater(dice[1].Body.velocity.sqrMagnitude, 0.01f);
            Assert.AreEqual(-Mathf.Sign(dice[0].Body.velocity.x), Mathf.Sign(dice[1].Body.velocity.x));
        }
    }
}

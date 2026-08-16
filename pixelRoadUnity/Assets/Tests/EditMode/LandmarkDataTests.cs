using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PixelRoad.Tests.EditMode
{
    public sealed class LandmarkDataTests
    {
        private const string ParserTypeName = "PixelRoad.Data.LandmarkJsonParser, Assembly-CSharp";
        private const string RepositoryTypeName = "PixelRoad.Data.VisitRepository, Assembly-CSharp";
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "PixelRoad-VisitTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(temporaryDirectory) && Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void LandmarksJson_UsesExpectedRootArrayAndFields()
        {
            TextAsset asset = Resources.Load<TextAsset>("PixelRoad/landmarks");
            Assert.That(asset, Is.Not.Null);

            IList landmarks = Parse(asset.text);
            Assert.That(landmarks, Has.Count.EqualTo(83));
            Assert.That(ReadProperty<int>(landmarks[0], "LandmarkId"), Is.EqualTo(1));
            Assert.That(ReadProperty<string>(landmarks[0], "DisplayName"), Is.EqualTo("테스트 기준점"));
            Assert.That(ReadProperty<string[]>(landmarks[0], "Tags"), Does.Contain("GPS"));
            Assert.That(ReadProperty<string>(landmarks[9], "CollectionTitle"), Is.EqualTo("조선"));
            Assert.That(ReadProperty<float>(landmarks[0], "RadiusMeters"), Is.EqualTo(80f));
        }

        [Test]
        public void LandmarksJson_RejectsDuplicateIds()
        {
            const string json = "["
                + "{\"id\":1,\"name\":\"A\",\"latitude\":37,\"longitude\":127,\"visitRadius\":50},"
                + "{\"id\":1,\"name\":\"B\",\"latitude\":37,\"longitude\":127,\"visitRadius\":50}"
                + "]";

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => Parse(json));
            Assert.That(exception.InnerException, Is.TypeOf<FormatException>());
        }

        [Test]
        public void VisitRepository_CountsAtMostOncePerLocalDateAndReloads()
        {
            Type repositoryType = Type.GetType(RepositoryTypeName, true);
            string fileName = (string)repositoryType.GetField("FileName", BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();
            string filePath = Path.Combine(temporaryDirectory, fileName);
            object repository = Activator.CreateInstance(repositoryType, filePath);
            DateTime firstVisit = new DateTime(2026, 8, 1, 10, 20, 0, DateTimeKind.Local);

            Assert.That(RecordVisit(repository, 1, firstVisit), Is.True);
            Assert.That(RecordVisit(repository, 1, firstVisit.AddHours(8)), Is.False);
            Assert.That(RecordVisit(repository, 1, firstVisit.AddDays(2).AddHours(5)), Is.True);

            IList records = ReadRecords(repository);
            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(ReadField<int>(records[0], "visitCount"), Is.EqualTo(2));
            Assert.That(ReadField<string>(records[0], "firstVisitedAt"), Is.EqualTo("2026-08-01T10:20:00"));
            Assert.That(ReadField<string>(records[0], "lastVisitedAt"), Is.EqualTo("2026-08-03T15:20:00"));
            Assert.That(File.ReadAllText(filePath).TrimStart(), Does.StartWith("["));

            object reloaded = Activator.CreateInstance(repositoryType, filePath);
            Assert.That(Invoke<bool>(reloaded, "HasVisited", 1), Is.True);
            Assert.That(ReadField<int>(ReadRecords(reloaded)[0], "visitCount"), Is.EqualTo(2));
        }

        [Test]
        public void VisitRepository_DoesNotCountWhenClockMovesBackwards()
        {
            Type repositoryType = Type.GetType(RepositoryTypeName, true);
            string filePath = Path.Combine(temporaryDirectory, "visited_landmarks.json");
            object repository = Activator.CreateInstance(repositoryType, filePath);

            Assert.That(
                RecordVisit(repository, 7, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Local)),
                Is.True);
            Assert.That(
                RecordVisit(repository, 7, new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Local)),
                Is.False);
            Assert.That(ReadField<int>(ReadRecords(repository)[0], "visitCount"), Is.EqualTo(1));
        }

        private static IList Parse(string json)
        {
            Type parserType = Type.GetType(ParserTypeName, true);
            MethodInfo parse = parserType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
            Assert.That(parse, Is.Not.Null);
            return (IList)parse.Invoke(null, new object[] { json, 50f });
        }

        private static bool RecordVisit(object repository, int landmarkId, DateTime time)
        {
            return Invoke<bool>(repository, "RecordVisit", landmarkId, time);
        }

        private static IList ReadRecords(object repository)
        {
            object value = repository.GetType().GetProperty("Records").GetValue(repository);
            Assert.That(value, Is.AssignableTo<IList>());
            return (IList)value;
        }

        private static T Invoke<T>(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return (T)method.Invoke(target, arguments);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(target);
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }
    }
}

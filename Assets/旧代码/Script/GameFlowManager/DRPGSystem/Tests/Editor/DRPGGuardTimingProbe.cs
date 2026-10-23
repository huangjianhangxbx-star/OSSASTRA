#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;

public static class DRPGGuardTimingProbe
{
    public static string Run()
    {
        Type sequenceType = Type.GetType(
            "DRPGGuardTimingSequence, Assembly-CSharp");
        AssertNotNull(sequenceType,
            "Removing the guard timing sequence would make defense grading presentation-dependent");

        object sequence = Activator.CreateInstance(
            sequenceType,
            new object[]
            {
                new[] { 0.65f, 1.10f, 1.55f },
                0.42f,
                0.12f
            });
        MethodInfo submit = sequenceType.GetMethod("SubmitGuard");
        MethodInfo advance = sequenceType.GetMethod("Advance");
        PropertyInfo complete = sequenceType.GetProperty("IsComplete");
        AssertNotNull(submit, "Guard input must have one explicit submission interface");
        AssertNotNull(advance, "Guard timing must advance from unscaled elapsed time");
        AssertNotNull(complete, "Callers must be able to observe sequence completion");

        AssertFalse(InvokeBool(submit, sequence, 0.20f),
            "A press earlier than the 0.42 second guard window must be rejected");
        AssertTrue(InvokeBool(submit, sequence, 0.23f),
            "The inclusive normal-guard boundary must accept input");
        AssertFalse(InvokeBool(submit, sequence, 0.30f),
            "One impact cannot consume multiple guard presses");
        AssertGrade("Guard", SingleDecision(advance, sequence, 0.65f));

        AssertTrue(InvokeBool(submit, sequence, 0.98f),
            "The inclusive 0.12 second perfect boundary must accept input");
        AssertGrade("Perfect", SingleDecision(advance, sequence, 1.10f));

        AssertGrade("Miss", SingleDecision(advance, sequence, 1.55f));
        AssertTrue((bool)complete.GetValue(sequence),
            "All three impacts must settle the timing sequence");
        AssertEqual(0, ((IList)advance.Invoke(sequence, new object[] { 2f })).Count,
            "Advancing a completed sequence must not duplicate impacts");

        return "PASS DRPG guard timing";
    }

    private static object SingleDecision(MethodInfo advance, object sequence, float elapsed)
    {
        IList decisions = (IList)advance.Invoke(sequence, new object[] { elapsed });
        AssertEqual(1, decisions.Count,
            "Each configured impact must emit exactly one decision");
        return decisions[0];
    }

    private static void AssertGrade(string expected, object decision)
    {
        PropertyInfo grade = decision.GetType().GetProperty("Grade");
        AssertNotNull(grade, "A guard impact decision must expose its grade");
        AssertEqual(expected, grade.GetValue(decision).ToString(),
            "Guard timing selected the wrong grade");
    }

    private static bool InvokeBool(MethodInfo method, object target, float value)
    {
        return (bool)method.Invoke(target, new object[] { value });
    }

    private static void AssertNotNull(object value, string message)
    {
        if (value == null)
            throw new Exception(message);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif

namespace DigitalSingularity.Lua.Test;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework.Constraints;
using NUnit.Framework.Internal;

internal static class TestExtensions
{
    //     /// <summary>
    // /// The Assert class contains a collection of static methods that
    // /// implement the most common assertions used in NUnit.
    // /// </summary>
    extension(Assert)
    {
        public static unsafe void That(
            void* actual,
            IResolveConstraint expression,
            NUnitString message = default,
            [CallerArgumentExpression(nameof(actual))]
            string actualExpression = "",
            [CallerArgumentExpression(nameof(expression))]
            string constraintExpression = "")
        {
            IConstraint constraint = expression.Resolve();

            TestExecutionContext.CurrentContext.IncrementAssertCount();
            ConstraintResult result = ApplyConstraint(constraint, actual);
            if (!result.IsSuccess)
            {
                ReportFailure(null!, result, message.ToString(), actualExpression, constraintExpression);
            }
        }
        
        public static unsafe void That<TActual>(
            TActual* actual,
            IResolveConstraint expression,
            NUnitString message = default,
            [CallerArgumentExpression(nameof(actual))]
            string actualExpression = "",
            [CallerArgumentExpression(nameof(expression))]
            string constraintExpression = "")
            where TActual : unmanaged
        {
            IConstraint constraint = expression.Resolve();

            TestExecutionContext.CurrentContext.IncrementAssertCount();
            ConstraintResult result = ApplyConstraint(constraint, actual);
            if (!result.IsSuccess)
            {
                ReportFailure(null!, result, message.ToString(), actualExpression, constraintExpression);
            }
        }
    }

    extension(Is)
    {
        public static unsafe EqualConstraint EqualTo(void* expected)
        {
            return new EqualConstraint((nint)expected);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReportFailure")]
    public static extern void ReportFailure(
        Assert ignored,
        ConstraintResult result,
        string message,
        string actualExpression,
        string constraintExpression);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_BaseConstraint")]
    public static extern IConstraint BaseConstraint(PrefixConstraint obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_expected")]
    public static extern ref object? GetExpected(EqualConstraint constraint);

    private static unsafe ConstraintResult ApplyConstraint(IConstraint constraint, void* actual)
    {
        if (constraint is NotConstraint notConstraint)
        {
            ConstraintResult baseResult = ApplyConstraint(BaseConstraint(notConstraint), actual);
            return new ConstraintResult(constraint, baseResult.ActualValue, !baseResult.IsSuccess);
        }
            
        if (constraint is NullConstraint)
        {
            return new ConstraintResult(constraint, (nint)actual, actual is null);
        }

        if (constraint is EqualConstraint equalConstraint)
        {
            return new EqualConstraintResult(
                equalConstraint,
                (nint)actual,
                (void*)(nint)(GetExpected(equalConstraint) ?? (nint)0) == actual);
        }
            
        throw new NotImplementedException();
    }

    extension(ReadOnlySpan<byte> ptr)
    {
        public unsafe byte* ToPointer()
        {
            ref byte r0 = ref MemoryMarshal.GetReference(ptr);
            return (byte*)Unsafe.AsPointer(ref r0);
        }
    }
}

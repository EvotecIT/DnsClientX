#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis {
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute {
        public NotNullWhenAttribute(bool returnValue) {
            ReturnValue = returnValue;
        }
        public bool ReturnValue { get; }
    }
}
#endif

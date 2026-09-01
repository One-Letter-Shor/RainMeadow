namespace RainMeadow;

public static class PhysicalObjectExtensions
{
    extension (PhysicalObject self)
    {
        /// <inheritdoc cref="AbstractPhysicalObjectExtensions.extension(AbstractPhysicalObject).IsMine"/>
        public bool IsMine => self.abstractPhysicalObject.IsMine;
    }
}

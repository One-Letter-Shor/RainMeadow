using System;

namespace RainMeadow;

public static class AbstractPhysicalObjectExtensions
{
    extension (AbstractPhysicalObject self)
    {
        /// <value>
        /// <see langword="true"/> if the <see cref="OnlinePhysicalObject"/> is not
        /// found; otherwise, the value of <see cref="OnlinePhysicalObject.isMine"/>.
        /// </value>
        /// <exception cref="InvalidOperationException">Thrown when not in a <see cref="Lobby"/>.</exception>
        public bool IsMine
        {
            get
            {
                if (OnlineManager.lobby is null)
                    throw new InvalidOperationException("Not in a lobby.");

                if (!self.GetOnlineObject(out OnlinePhysicalObject opo))
                    return true;

                return opo.isMine;
            }
        }
    }
}

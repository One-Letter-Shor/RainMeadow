using System;
using RainMeadow;

namespace Drown
{
    public class ArenaDrownClientSettings : OnlineEntity.EntityData
    {
        public bool isInStore;
        public bool iOpenedDen;

        public ArenaDrownClientSettings() { }

        public override EntityDataState MakeState(OnlineEntity entity, OnlineResource inResource)
        {
            return new State(this);
        }

        public class State : EntityDataState
        {
            [OnlineField]
            public bool isInStore;
            [OnlineField]
            public bool iOpenedDen;
            public State() { }

            public State(ArenaDrownClientSettings clientData)
            {
                isInStore = clientData.isInStore;
                iOpenedDen = clientData.iOpenedDen;
            }

            public override void ReadTo(OnlineEntity.EntityData entityData, OnlineEntity onlineEntity)
            {
                ArenaDrownClientSettings clientData = (ArenaDrownClientSettings)entityData;

                clientData.isInStore = isInStore;
                clientData.iOpenedDen = iOpenedDen;
            }

            public override Type GetDataType() => typeof(ArenaDrownClientSettings);
        }
    }
}

﻿using OpenNos.GameObject.Buff.BCard;

namespace OpenNos.GameObject.Buff.Indicators.SP3.Swordsman
{
    public class PrayerofOffence : IndicatorBase
    {
        #region Instantiation

        public PrayerofOffence(int Level)
        {
            Name = "Prayer of Defence";
            Duration = 1800;
            Id = 139;
            _level = Level;
            DirectBuffs.Add(new BCardEntry(Type.Damage, SubType.IncreaseLevel, 2, 0, false));
            DirectBuffs.Add(new BCardEntry(Type.Morale, SubType.Increase, 5, 0, false));
        }

        #endregion
    }
}
﻿using GoodAI.Modules.School.Common;
using GoodAI.Modules.School.Worlds;
using System;
using System.ComponentModel;
using System.Drawing;

namespace GoodAI.Modules.School.LearningTasks
{
    [DisplayName("LTSCT6")]
    public class Ltsct6 : Ltsct1
    {
        private readonly Random m_rndGen = new Random();

        public Ltsct6() : this(null) { }

        public Ltsct6(SchoolWorld w)
            : base(w)
        {
            TSHints = new TrainingSetHints {
                { TSHintAttributes.MAX_NUMBER_OF_ATTEMPTS, 1000000 },
                { TSHintAttributes.IMAGE_NOISE, 1},
                { TSHintAttributes.IMAGE_NOISE_BLACK_AND_WHITE, 1}
            };

            TSProgression.Add(TSHints.Clone());
        }

        protected override bool DidTrainingUnitComplete(ref bool wasUnitSuccessful)
        {
            wasUnitSuccessful = false;

            return true;
        }

        
        protected override void CreateScene()
        {
            SizeF size = new SizeF(WrappedWorld.GetPowGeometry().Width / 4, WrappedWorld.GetPowGeometry().Height / 4);

            int positionsCount = m_positions.Positions.Count;
            int randomLocationIdx = m_rndGen.Next(positionsCount);
            PointF location = m_positions.Positions[randomLocationIdx];

            int randomLocationIdx2 = (m_rndGen.Next(positionsCount - 1) + randomLocationIdx + 1) % positionsCount;
            PointF location2 = m_positions.Positions[randomLocationIdx2];

            WrappedWorld.CreateRandomEnemy(location, size, m_rndGen);

            if (LearningTaskHelpers.FlipCoin(m_rndGen))
            {
                WrappedWorld.CreateRandomFood(location2, size, m_rndGen);
            }
            else
            {
                WrappedWorld.CreateRandomStone(location2, size, m_rndGen);
            }
        }
    }
}

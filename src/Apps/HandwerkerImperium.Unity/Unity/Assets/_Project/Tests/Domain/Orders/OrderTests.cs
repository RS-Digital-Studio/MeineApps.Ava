using System;
using System.Collections.Generic;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Orders;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Orders
{
    /// <summary>
    /// Verifiziert das portierte Order-Subsystem (OrderType, OrderDifficulty, MiniGameType/Rating, Order)
    /// gegen die Original-Werte (Avalonia Models/Order.cs + Enums).
    /// </summary>
    [TestFixture]
    public class OrderTests
    {
        [Test]
        public void OrderType_Multipliers_MatchOriginal()
        {
            Assert.That(OrderType.Standard.GetTaskCount(), Is.EqualTo((2, 3)));
            Assert.That(OrderType.Weekly.GetRewardMultiplier(), Is.EqualTo(3.0m));
            Assert.That(OrderType.Cooperation.GetRewardMultiplier(), Is.EqualTo(2.5m));
            Assert.That(OrderType.Large.GetXpMultiplier(), Is.EqualTo(2.0m));
            Assert.That(OrderType.Large.GetUnlockLevel(), Is.EqualTo(10));
            Assert.That(OrderType.Weekly.HasDeadline(), Is.True);
            Assert.That(OrderType.Weekly.GetDeadline(), Is.EqualTo(TimeSpan.FromDays(7)));
            Assert.That(OrderType.Cooperation.RequiresMultipleWorkshops(), Is.True);
            Assert.That(OrderType.MaterialOrder.IsMaterialOrder(), Is.True);
        }

        [Test]
        public void OrderDifficulty_Multipliers_MatchOriginal()
        {
            Assert.That(OrderDifficulty.Hard.GetRewardMultiplier(), Is.EqualTo(3.5m));
            Assert.That(OrderDifficulty.Expert.GetRewardMultiplier(), Is.EqualTo(5.0m));
            Assert.That(OrderDifficulty.Expert.GetXpMultiplier(), Is.EqualTo(5.5m));
            Assert.That(OrderDifficulty.Easy.GetPerfectZoneSize(), Is.EqualTo(0.20));
            Assert.That(OrderDifficulty.Expert.GetSpeedMultiplier(), Is.EqualTo(2.2));
            Assert.That(OrderDifficulty.Expert.GetRequiredReputation(), Is.EqualTo(80));
            Assert.That(OrderDifficulty.Easy.GetRequiredReputation(), Is.EqualTo(0));
        }

        [Test]
        public void MiniGameRating_Percentages_MatchOriginal()
        {
            Assert.That(MiniGameRating.Miss.GetRewardPercentage(), Is.EqualTo(0.20m));
            Assert.That(MiniGameRating.Ok.GetRewardPercentage(), Is.EqualTo(0.50m));
            Assert.That(MiniGameRating.Good.GetRewardPercentage(), Is.EqualTo(1.00m));
            Assert.That(MiniGameRating.Perfect.GetRewardPercentage(), Is.EqualTo(1.50m));
            Assert.That(MiniGameRating.Perfect.GetXpPercentage(), Is.EqualTo(1.50m));
        }

        [Test]
        public void MiniGameType_WorkshopMapping_MatchesOriginal()
        {
            Assert.That(MiniGameType.ForgeGame.GetWorkshopTypes(), Is.EqualTo(new[] { WorkshopType.MasterSmith }));
            Assert.That(MiniGameType.Measuring.GetWorkshopTypes(), Is.EqualTo(new[] { WorkshopType.Contractor, WorkshopType.Carpenter }));
            Assert.That(MiniGameType.InventGame.GetWorkshopTypes(), Is.EqualTo(new[] { WorkshopType.InnovationLab }));
        }

        [Test]
        public void Order_FinalReward_AppliesAllMultipliers()
        {
            var order = new Order { BaseReward = 100m, Difficulty = OrderDifficulty.Medium, OrderType = OrderType.Standard, Strategy = OrderStrategy.Standard };
            Assert.That(order.FinalReward, Is.EqualTo(0m)); // keine Tasks
            order.RecordTaskResult(MiniGameRating.Good); // 1.0
            // 100 * 1.0 (Good) * 1.5 (Medium) * 1.0 (Standard-Type) * 1.0 (Standard-Strategy)
            Assert.That(order.FinalReward, Is.EqualTo(150m));
            Assert.That(order.CurrentTaskIndex, Is.EqualTo(1));
        }

        [Test]
        public void Order_PerfectAndRisk_ScaleReward()
        {
            var perfect = new Order { BaseReward = 100m, Difficulty = OrderDifficulty.Medium, OrderType = OrderType.Standard, Strategy = OrderStrategy.Standard };
            perfect.RecordTaskResult(MiniGameRating.Perfect); // 1.5
            Assert.That(perfect.FinalReward, Is.EqualTo(225m)); // 100*1.5*1.5

            var risk = new Order { BaseReward = 100m, Difficulty = OrderDifficulty.Easy, OrderType = OrderType.Standard, Strategy = OrderStrategy.Risk };
            risk.RecordTaskResult(MiniGameRating.Good);
            Assert.That(risk.FinalReward, Is.EqualTo(200m)); // 100*1.0*1.0*1.0*2.0
        }

        [Test]
        public void Order_HardFail_YieldsZero()
        {
            var order = new Order { BaseReward = 100m, Difficulty = OrderDifficulty.Hard, Strategy = OrderStrategy.Risk, HasHardFailed = true };
            order.RecordTaskResult(MiniGameRating.Perfect);
            Assert.That(order.FinalReward, Is.EqualTo(0m));
            Assert.That(order.FinalXp, Is.EqualTo(0));
        }

        [Test]
        public void Order_Expiry_And_MaterialOffer_And_Completion()
        {
            Assert.That(new Order { Deadline = DateTime.UtcNow.AddMinutes(-1) }.IsExpired, Is.True);
            Assert.That(new Order { Deadline = DateTime.UtcNow.AddHours(1) }.IsExpired, Is.False);
            Assert.That(new Order { MaterialOffer = new Dictionary<string, int> { { "planks", 3 } } }.HasMaterialOffer, Is.True);
            Assert.That(new Order().HasMaterialOffer, Is.False);

            var twoTask = new Order { Tasks = new List<OrderTask> { new OrderTask(), new OrderTask() } };
            Assert.That(twoTask.IsCompleted, Is.False);
            twoTask.RecordTaskResult(MiniGameRating.Good);
            twoTask.RecordTaskResult(MiniGameRating.Good);
            Assert.That(twoTask.IsCompleted, Is.True);
        }
    }
}

﻿using System;
using System.Linq;
using System.Collections.Generic;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    class Kayle : Common.Helper
    {
        private class RAntiItem
        {
            public float StartTick;
            public float EndTick;
            public RAntiItem(BuffInstance Buff)
            {
                StartTick = Game.Time + (Buff.EndTime - Buff.StartTime) - (R.Level * 0.5f + 1);
                EndTick = Game.Time + (Buff.EndTime - Buff.StartTime);
            }
        }
        private Dictionary<int, RAntiItem> RAntiDetected = new Dictionary<int, RAntiItem>();

        public Kayle()
        {
            Q = new Spell(SpellSlot.Q, 657.5f, TargetSelector.DamageType.Magical);
            W = new Spell(SpellSlot.W, 904.35f);
            E = new Spell(SpellSlot.E, 525, TargetSelector.DamageType.Magical);
            R = new Spell(SpellSlot.R, 915.95f);
            Q.SetTargetted(0.5f, 1500);
            W.SetTargetted(0.3333f, float.MaxValue);
            R.SetTargetted(0.5f, float.MaxValue);

            var ChampMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    var HealMenu = new Menu("Heal (W)", "Heal");
                    {
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsAlly))
                        {
                            var Name = Obj.IsMe ? "Self" : Obj.ChampionName;
                            AddItem(HealMenu, Name, Name);
                            AddItem(HealMenu, Name + "HpU", "-> If Hp Under", 40);
                        }
                        ComboMenu.AddSubMenu(HealMenu);
                    }
                    var SaveMenu = new Menu("Save (R)", "Save");
                    {
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsAlly))
                        {
                            var Name = Obj.IsMe ? "Self" : Obj.ChampionName;
                            AddItem(SaveMenu, Name, Name);
                            AddItem(SaveMenu, Name + "HpU", "-> If Hp Under", 30);
                        }
                        ComboMenu.AddSubMenu(SaveMenu);
                    }
                    var AntiMenu = new Menu("Anti (R)", "Anti");
                    {
                        AddItem(AntiMenu, "Zed", "Zed");
                        AddItem(AntiMenu, "Fizz", "Fizz");
                        AddItem(AntiMenu, "Vlad", "Vladimir");
                        AddItem(AntiMenu, "Karthus", "Karthus");
                        ComboMenu.AddSubMenu(AntiMenu);
                    }
                    AddItem(ComboMenu, "Q", "Use Q");
                    AddItem(ComboMenu, "W", "Use W");
                    AddItem(ComboMenu, "WSpeed", "-> Speed");
                    AddItem(ComboMenu, "WHeal", "-> Heal");
                    AddItem(ComboMenu, "E", "Use E");
                    AddItem(ComboMenu, "EAoE", "-> Focus Most AoE Target");
                    AddItem(ComboMenu, "R", "Use R");
                    AddItem(ComboMenu, "RSave", "-> Save");
                    AddItem(ComboMenu, "RAnti", "-> Anti Dangerous Ultimate", new[] { "Off", "Self", "Ally", "Both" }, 3);
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(HarassMenu, "AutoQ", "Use Q", "H", KeyBindType.Toggle);
                    AddItem(HarassMenu, "AutoQMpA", "-> If Mp Above", 50);
                    AddItem(HarassMenu, "Q", "Use Q");
                    AddItem(HarassMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    var SmiteMob = new Menu("Smite Mob If Killable", "SmiteMob");
                    {
                        AddItem(SmiteMob, "Smite", "Use Smite");
                        AddItem(SmiteMob, "Baron", "-> Baron Nashor");
                        AddItem(SmiteMob, "Dragon", "-> Dragon");
                        AddItem(SmiteMob, "Red", "-> Red Brambleback");
                        AddItem(SmiteMob, "Blue", "-> Blue Sentinel");
                        AddItem(SmiteMob, "Krug", "-> Ancient Krug");
                        AddItem(SmiteMob, "Gromp", "-> Gromp");
                        AddItem(SmiteMob, "Raptor", "-> Crimson Raptor");
                        AddItem(SmiteMob, "Wolf", "-> Greater Murk Wolf");
                        ClearMenu.AddSubMenu(SmiteMob);
                    }
                    AddItem(ClearMenu, "Q", "Use Q");
                    AddItem(ClearMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var FleeMenu = new Menu("Flee", "Flee");
                {
                    AddItem(FleeMenu, "Q", "Use Q To Slow Enemy");
                    AddItem(FleeMenu, "W", "Use W");
                    ChampMenu.AddSubMenu(FleeMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    var KillStealMenu = new Menu("Kill Steal", "KillSteal");
                    {
                        AddItem(KillStealMenu, "Q", "Use Q");
                        AddItem(KillStealMenu, "Ignite", "Use Ignite");
                        MiscMenu.AddSubMenu(KillStealMenu);
                    }
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    AddItem(DrawMenu, "Q", "Q Range", false);
                    AddItem(DrawMenu, "W", "W Range", false);
                    AddItem(DrawMenu, "R", "R Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                MainMenu.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
        }

        private void OnGameUpdate(EventArgs args)
        {
            AntiDetect();
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee) Flee();
            if (GetValue<KeyBind>("Harass", "AutoQ").Active) AutoQ();
            KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (GetValue<bool>("Draw", "Q") && Q.Level > 0) Render.Circle.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "W") && W.Level > 0) Render.Circle.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "R") && R.Level > 0) Render.Circle.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void NormalCombo(string Mode)
        {
            if (Mode == "Combo" && GetValue<bool>(Mode, "E") && GetValue<bool>(Mode, "EAoE") && Player.HasBuff("JudicatorRighteousFury"))
            {
                var Target = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => Orbwalk.InAutoAttackRange(i)).MaxOrDefault(i => i.CountEnemysInRange(150));
                if (Target != null) Orbwalk.ForcedTarget = Target;
            }
            else Orbwalk.ForcedTarget = null;
            if (GetValue<bool>(Mode, "Q") && Q.CastOnBestTarget(0, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
            if ((!GetValue<bool>(Mode, "Q") || (GetValue<bool>(Mode, "Q") && !Q.IsReady())) && GetValue<bool>(Mode, "E") && E.IsReady() && E.GetTarget() != null && E.Cast(PacketCast)) return;
            if (Mode == "Combo")
            {
                if (GetValue<bool>(Mode, "W") && W.IsReady())
                {
                    if (GetValue<bool>(Mode, "WHeal"))
                    {
                        var Obj = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => i.IsValidTarget(W.Range, false) && i.IsAlly && GetValue<bool>("Heal", MenuName(i)) && i.HealthPercentage() < GetValue<Slider>("Heal", MenuName(i) + "HpU").Value && !i.InFountain() && !i.IsRecalling() && i.CountEnemysInRange(W.Range) >= 1 && !i.HasBuff("JudicatorIntervention") && !i.HasBuff("Undying Rage")).MinOrDefault(i => i.Health);
                        if (Obj != null && W.Cast(Obj, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
                    }
                    if (GetValue<bool>(Mode, "WSpeed"))
                    {
                        var Target = Q.GetTarget(200);
                        if (Target != null && !Target.IsFacing(Player) && (!Player.HasBuff("JudicatorRighteousFury") || (Player.HasBuff("JudicatorRighteousFury") && !Orbwalk.InAutoAttackRange(Target))) && (!GetValue<bool>(Mode, "Q") || (GetValue<bool>(Mode, "Q") && Q.IsReady() && !Q.IsInRange(Target))) && W.Cast(PacketCast)) return;
                    }
                }
                if (GetValue<bool>(Mode, "R") && R.IsReady())
                {
                    if (GetValue<bool>(Mode, "RSave"))
                    {
                        var Obj = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => i.IsValidTarget(R.Range, false) && i.IsAlly && GetValue<bool>("Save", MenuName(i)) && i.HealthPercentage() < GetValue<Slider>("Save", MenuName(i) + "HpU").Value && !i.InFountain() && !i.IsRecalling() && i.CountEnemysInRange(W.Range) >= 1 && !i.HasBuff("Undying Rage")).MinOrDefault(i => i.Health);
                        if (Obj != null && R.Cast(Obj, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
                    }
                    if (GetValue<StringList>(Mode, "RAnti").SelectedIndex > 0)
                    {
                        var Obj = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => i.IsValidTarget(R.Range, false) && i.IsAlly && RAntiDetected.ContainsKey(i.NetworkId) && Game.Time > RAntiDetected[i.NetworkId].StartTick && !i.HasBuff("Undying Rage")).MinOrDefault(i => i.Health);
                        if (Obj != null && R.Cast(Obj, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
                    }
                }
            }
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            if (minionObj.Count == 0) return;
            if (GetValue<bool>("SmiteMob", "Smite") && minionObj.Any(i => i.Team == GameObjectTeam.Neutral && CanSmiteMob(i.Name) && CastSmite(i))) return;
            if (GetValue<bool>("Clear", "Q") && Q.IsReady())
            {
                var Obj = minionObj.Find(i => CanKill(i, Q));
                if (Obj == null) Obj = minionObj.Find(i => i.MaxHealth >= 1200);
                if (Obj != null && Q.Cast(Obj, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
            }
            if (GetValue<bool>("Clear", "E") && E.IsReady() && (minionObj.Count >= 2 || minionObj.Count(i => i.MaxHealth >= 1200) >= 1) && E.Cast(PacketCast)) return;
        }

        private void Flee()
        {
            if (GetValue<bool>("Flee", "W") && W.IsReady() && W.Cast(PacketCast)) return;
            if (GetValue<bool>("Flee", "Q") && Q.CastOnBestTarget(0, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
        }

        private void AutoQ()
        {
            if (Player.ManaPercentage() < GetValue<Slider>("Harass", "AutoQMpA").Value || Q.CastOnBestTarget(0, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
        }

        private void KillSteal()
        {
            if (GetValue<bool>("KillSteal", "Ignite") && Ignite.IsReady())
            {
                var Target = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.True);
                if (Target != null && CastIgnite(Target)) return;
            }
            if (GetValue<bool>("KillSteal", "Q") && Q.IsReady())
            {
                var Target = Q.GetTarget();
                if (Target != null && CanKill(Target, Q) && Q.Cast(Target, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
            }
        }

        private void AntiDetect()
        {
            if (Player.IsDead || GetValue<StringList>("Combo", "RAnti").SelectedIndex == 0 || R.Level == 0) return;
            var Key = ObjectManager.Get<Obj_AI_Hero>().Find(i => i.IsValidTarget(float.MaxValue, false) && i.IsAlly && RAntiDetected.ContainsKey(i.NetworkId) && Game.Time > RAntiDetected[i.NetworkId].EndTick);
            if (Key != null) RAntiDetected.Remove(Key.NetworkId);
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(float.MaxValue, false) && i.IsAlly && !RAntiDetected.ContainsKey(i.NetworkId)))
            {
                if ((GetValue<StringList>("Combo", "RAnti").SelectedIndex == 1 && Obj.IsMe) || (GetValue<StringList>("Combo", "RAnti").SelectedIndex == 2 && !Obj.IsMe) || GetValue<StringList>("Combo", "RAnti").SelectedIndex == 3)
                {
                    foreach (var Buff in Obj.Buffs)
                    {
                        if ((Buff.DisplayName == "ZedUltExecute" && GetValue<bool>("Anti", "Zed")) || (Buff.DisplayName == "FizzChurnTheWatersCling" && GetValue<bool>("Anti", "Fizz")) || (Buff.DisplayName == "VladimirHemoplagueDebuff" && GetValue<bool>("Anti", "Vlad")))
                        {
                            RAntiDetected.Add(Obj.NetworkId, new RAntiItem(Buff));
                        }
                        else if (Buff.DisplayName == "KarthusFallenOne" && GetValue<bool>("Anti", "Karthus") && Obj.Health <= ((Obj_AI_Hero)Buff.Caster).GetSpellDamage(Obj, SpellSlot.R) + Obj.Health * 0.2f && Obj.CountEnemysInRange(R.Range) >= 1) RAntiDetected.Add(Obj.NetworkId, new RAntiItem(Buff));
                    }
                }
            }
        }

        private string MenuName(Obj_AI_Hero Obj)
        {
            return Obj.IsMe ? "Self" : Obj.ChampionName;
        }
    }
}
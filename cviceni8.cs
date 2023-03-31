using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace ConsoleApp1
{
    using Event = EventQueue.Event;

    sealed class EventQueue
    {
        private interface IEventAccess
        {
            Event? Prev { get; set; }
            Event? Next { get; set; }
            ulong Time { get; set; }
            Event.EventState State { get; set; }
        }

        public abstract class Event : IEventAccess
        {
            public enum EventState
            {
                Created,
                Scheduled,
                Executed,
                Cancelled
            }

            private Event? prev;
            private Event? next;

            public ulong Time { get; private set; }
            public EventState State { get; private set; }

            public abstract void Execute();

            public Event() => State = EventState.Created;

            Event? IEventAccess.Prev { get => prev; set => prev = value; }
            Event? IEventAccess.Next { get => next; set => next = value; }
            ulong IEventAccess.Time { get => Time; set => Time = value; }
            EventState IEventAccess.State { get => State; set => State = value; }
        }

        private Event? head;
        private Event? tail;
        
        public ulong CurrentTime { get; private set; }

        private void Unlink(Event e)
        {
            IEventAccess ea = e;
            Event? prev = ea.Prev;
            Event? next = ea.Next;
            ea.Prev = null;
            ea.Next = null;

            if (prev != null)
            {
                (prev as IEventAccess).Next = next;
            }
            else
            {
                head = next;
            }

            if (next != null)
            {
                (next as IEventAccess).Prev = prev;
            }
            else
            {
                tail = prev;
            }
        }

        private void Link(Event e, Event? prev, Event? next)
        {
            IEventAccess ea = e;
            ea.Prev = prev;
            ea.Next = next;

            if (prev != null)
            {
                (prev as IEventAccess).Next = e;
            }
            else
            {
                head = e;
            }

            if (next != null)
            {
                (next as IEventAccess).Prev = e;
            }
            else
            {
                tail = e;
            }
        }

        private void LinkAfter(Event e, Event? prev) => Link(e, prev, prev != null ? (prev as IEventAccess).Next : head);
        private void LinkBefore(Event e, Event? next) => Link(e, next != null ? (next as IEventAccess).Prev : tail, next);

        public void AddEvent(ulong timeDiff, Event e)
        {
            if (e.State == Event.EventState.Scheduled)
            {
                throw new InvalidOperationException("Cannot schedule already scheduled event");
            }

            var time = CurrentTime + timeDiff;
            var prev = tail;
            while (prev != null && prev.Time > time)
            {
                prev = (prev as IEventAccess).Prev;
            }
            LinkAfter(e, prev);

            IEventAccess ea = e;
            ea.State = Event.EventState.Scheduled;
            ea.Time = time;
        }

        public void MoveEvent(ulong timeDiff, Event e)
        {
            if (e == null || e.State != Event.EventState.Scheduled)
            {
                return;
            }

            IEventAccess ea = e;
            var time = CurrentTime + timeDiff;
            if (time < e.Time)
            {
                var prev = ea.Prev;
                while (prev != null && prev.Time > time)
                {
                    prev = (prev as IEventAccess).Prev;
                }
                if (prev != ea.Prev)
                {
                    Unlink(e);
                    LinkAfter(e, prev);
                }
            }
            else if (time > e.Time)
            {
                var next = ea.Next;
                while (next != null && next.Time <= time)
                {
                    next = (next as IEventAccess).Next;
                }
                if (next != ea.Next)
                {
                    Unlink(e);
                    LinkBefore(e, next);
                }
            }

            ea.Time = time;
        }

        public void RemoveEvent(Event e)
        {
            if (e.State != Event.EventState.Scheduled)
            {
                return;
            }

            Unlink(e);
            (e as IEventAccess).State = Event.EventState.Cancelled;
        }

        public void ExecuteUntil(ulong endTime)
        {
            while (head != null && head.Time < endTime)
            {
                Event e = head;
                Unlink(e);
                CurrentTime = e.Time;
                (e as IEventAccess).State = Event.EventState.Executed;
                e.Execute();
            }
        }

        public void Reset()
        {
            CurrentTime = 0;
            while (head != null)
            {
                RemoveEvent(head);
            }
        }
    }

    class Cooldown
    {
        public string Name { get; protected set; }
        protected Sim sim;
        protected ulong duration;
        protected ulong recharge;

        public Cooldown(string name, Sim sim, ulong duration)
        {
            Name = name;
            this.sim = sim;
            this.duration = duration;
        }

        public virtual bool Ready() => sim.CurrentTime >= recharge;

        public virtual void Start()
        {
            if (!Ready())
            {
                throw new InvalidOperationException("Cooldown must be ready before starting");
            }

            recharge = sim.CurrentTime + duration;
            sim.Log.Message($"Starting {Name}, will be ready at {recharge} ms");
        }

        public virtual void Reset() => recharge = 0;
    }

    class Buff
    {
        private class ExpirationEvent : Event
        {
            private Buff buff;

            public ExpirationEvent(Buff b) => buff = b;

            public override void Execute() => buff.OnExpiration();
        }

        public string Name { get; protected set; }
        protected Sim sim;
        protected int maxStack;
        protected ulong duration;
        protected Event? expiration;

        public int Stack { get; protected set; }

        public Buff(string name, Sim sim, int maxStack, ulong duration)
        {
            Name = name;
            this.sim = sim;
            this.maxStack = maxStack;
            this.duration = duration;
        }

        protected virtual void OnExpiration()
        {
            Stack = 0;
            expiration = null;

            sim.Log.Message($"Expiring {Name}");
        }

        public virtual void Start()
        {
            int oldStack = Stack;
            Stack = Math.Min(Stack + 1, maxStack);

            if (expiration != null)
            {
                sim.Events.MoveEvent(duration, expiration);
            }
            else
            {
                expiration = new ExpirationEvent(this);
                sim.Events.AddEvent(duration, expiration);
            }

            sim.Log.Message($"{(oldStack > 0 ? "Refreshing" : "Starting")} {Name} ({Stack}/{maxStack}), will expire at {expiration.Time} ms");
        }

        public virtual void Decrement()
        {
            if (Stack == 0)
            {
                return;
            }

            if (Stack == 1)
            {
                Expire();
            }
            else
            {
                Stack--;
                sim.Log.Message($"Decrementing {Name} ({Stack}/{maxStack})");
            }
        }

        public virtual void Expire()
        {
            if (Stack == 0)
            {
                return;
            }

            if (expiration != null)
            {
                sim.Events.RemoveEvent(expiration);
            }

            OnExpiration();
        }

        public virtual void Reset()
        {
            Stack = 0;
            expiration = null;
        }
    }

    class Spell
    {
        public string Name { get; protected set; }
        protected Sim sim;
        protected ulong baseCastTime;
        protected int baseDamage;

        public Spell(string name, Sim sim, ulong baseCastTime, int baseDamage)
        {
            Name = name;
            this.sim = sim;
            this.baseCastTime = baseCastTime;
            this.baseDamage = baseDamage;
        }

        public virtual bool Ready() => true;

        public virtual ulong CastTime() => baseCastTime;

        public virtual int Damage() => baseDamage;

        public virtual ulong GlobalCooldown() => 1500;

        public virtual void Execute()
        {
            var dmg = Damage();
            sim.Stats.Add(Name, dmg);
            sim.Log.Message($"{Name} executes, hitting the enemy for {dmg} damage");
        }

        public virtual void Reset() { }
    }

    sealed class Frostbolt : Spell
    {
        public Frostbolt(Sim sim) : base("Frostbolt", sim, 2000, 300) { }

        public override void Execute()
        {
            base.Execute();
            if (sim.Rng.NextDouble() < 0.3)
            {
                sim.Buffs.FingersOfFrost.Start();
            }
        }
    }

    sealed class FireBlast : Spell
    {
        private Cooldown cooldown;

        public FireBlast(Sim sim) : base("Fire Blast", sim, 0, 250) => cooldown = new Cooldown("Fire Blast cd", sim, 8000);

        public override bool Ready() => cooldown.Ready() && base.Ready();

        public override void Execute()
        {
            base.Execute();
            cooldown.Start();
        }

        public override void Reset()
        {
            base.Reset();
            cooldown.Reset();
        }
    }

    sealed class IceLance : Spell
    {
        public IceLance(Sim sim) : base("Ice Lance", sim, 0, 200) { }

        public override int Damage()
        {
            var dmg = base.Damage();
            if (sim.Buffs.FingersOfFrost.Stack > 0)
            {
                dmg *= 3;
            }
            return dmg;
        }

        public override void Execute()
        {
            base.Execute();
            sim.Buffs.FingersOfFrost.Decrement();
        }
    }

    sealed class Logger
    {
        private Sim sim;
        private bool enabled;

        public Logger(Sim sim, bool enabled)
        {
            this.sim = sim;
            this.enabled = enabled;
        }

        public void Message(string what)
        {
            if (enabled)
            {
                Console.Write($"{sim.CurrentTime,10} ms: ");
                Console.WriteLine(what);
            }
        }
    }

    sealed class Statistics
    {
        private Dictionary<string, int> data = new Dictionary<string, int>();

        public void Add(string name, int value)
        {
            if (data.ContainsKey(name))
            {
                data[name] += value;
            }
            else
            {
                data[name] = value;
            }
        }

        public double CalculateDPS(ulong time)
        {
            int total = 0;
            foreach (var kv in data)
            {
                total += kv.Value;
            }
            return 1000.0 * total / time;
        }

        public void Reset() => data.Clear();
    }

    interface IRotationLogic
    {
        Spell ChooseSpell(Sim sim);
        void Reset();
    }

    class NormalLogic : IRotationLogic
    {
        public Spell ChooseSpell(Sim sim)
        {
            if (sim.Buffs.FingersOfFrost.Stack > 0)
            {
                return sim.Spells.IceLance;
            }
            else
            {
                return sim.Spells.Frostbolt;
            }
        }

        public void Reset() { }
    }

    class FireBlastLogic : IRotationLogic
    {
        public Spell ChooseSpell(Sim sim)
        {
            if (sim.Spells.FireBlast.Ready())
            {
                return sim.Spells.FireBlast;
            }
            else if (sim.Buffs.FingersOfFrost.Stack > 0)
            {
                return sim.Spells.IceLance;
            }
            else
            {
                return sim.Spells.Frostbolt;
            }
        }

        public void Reset() { }
    }

    sealed class Sim
    {
        private class ActionEvent : Event
        {
            private Action action;

            public ActionEvent(Action action) => this.action = action;

            public override void Execute() => action();
        }

        public struct BuffList
        {
            public Buff FingersOfFrost;
        }

        public struct SpellList
        {
            public Spell Frostbolt;
            public Spell FireBlast;
            public Spell IceLance;
        }

        private struct SimState
        {
            public Event? ReadyEvent;
            public Event? CastEvent;
            public Spell? Casting;
            public ulong GcdReady;
        }

        private IRotationLogic logic;

        public EventQueue Events = new EventQueue();
        public Random Rng = new Random();
        public Statistics Stats = new Statistics();
        public Logger Log;

        public ulong CurrentTime => Events.CurrentTime;

        public BuffList Buffs;
        public SpellList Spells;
        private SimState state;

        public Sim(IRotationLogic logic, bool log = false)
        {
            this.logic = logic;
            Log = new Logger(this, log);

            Buffs.FingersOfFrost = new Buff("Fingers of Frost", this, 2, 15000);

            Spells.Frostbolt = new Frostbolt(this);
            Spells.FireBlast = new FireBlast(this);
            Spells.IceLance = new IceLance(this);
        }

        private void BeginCast()
        {
            state.ReadyEvent = null;
            Spell spell = logic.ChooseSpell(this);
            if (spell != null && spell.Ready())
            {
                ScheduleCast(spell);
                Log.Message($"Casting {spell.Name}");
            }
            else
            {
                ScheduleReady(100);
                Log.Message("No spell available, waiting");
            }
        }

        private void FinishCast()
        {
            state.CastEvent = null;
            state.Casting?.Execute();
            state.Casting = null;
            ScheduleReady();
        }

        private void ScheduleReady(ulong delay = 0)
        {
            ulong ready = Math.Max(CurrentTime + delay, state.GcdReady) - CurrentTime;
            state.ReadyEvent = new ActionEvent(BeginCast);
            Events.AddEvent(ready, state.ReadyEvent);
        }

        private void ScheduleCast(Spell spell)
        {
            state.Casting = spell;
            state.GcdReady = CurrentTime + spell.GlobalCooldown();
            ulong castTime = spell.CastTime();
            state.CastEvent = new ActionEvent(FinishCast);
            Events.AddEvent(castTime, state.CastEvent);
        }

        public void Reset()
        {
            logic.Reset();
            Events.Reset();
            Stats.Reset();

            Buffs.FingersOfFrost.Reset();

            Spells.Frostbolt.Reset();
            Spells.FireBlast.Reset();
            Spells.IceLance.Reset();

            state = default;
        }

        public void Run(ulong time)
        {
            Reset();
            ScheduleReady();
            Events.ExecuteUntil(time);
            Console.WriteLine($"Sim finished, final DPS: {Stats.CalculateDPS(time)}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Normal logic");
            Sim s1 = new Sim(new NormalLogic());
            s1.Run(100_000_000);

            Console.WriteLine("Fire Blast logic");
            Sim s2 = new Sim(new FireBlastLogic());
            s2.Run(100_000_000);

            Sim s3 = new Sim(new FireBlastLogic(), true);
            s3.Run(100_000);
        }
    }
}

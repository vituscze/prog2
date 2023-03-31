using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace ConsoleApp1
{
    abstract class Event
    {
        public enum EventState
        {
            Created,
            Scheduled,
            Executed,
            Cancelled
        }

        public Event? Prev;
        public Event? Next;

        public ulong Time;
        public EventState State;

        public abstract void Execute();

        public Event() => State = EventState.Created;
    }

    sealed class EventQueue
    {
        private Event? head;
        private Event? tail;
        
        public ulong CurrentTime { get; private set; }

        private void Unlink(Event e)
        {
            Event? prev = e.Prev;
            Event? next = e.Next;
            e.Prev = null;
            e.Next = null;

            if (prev != null)
            {
                prev.Next = next;
            }
            else
            {
                head = next;
            }

            if (next != null)
            {
                next.Prev = prev;
            }
            else
            {
                tail = prev;
            }
        }

        private void Link(Event e, Event? prev, Event? next)
        {
            e.Prev = prev;
            e.Next = next;

            if (prev != null)
            {
                prev.Next = e;
            }
            else
            {
                head = e;
            }

            if (next != null)
            {
                next.Prev = e;
            }
            else
            {
                tail = e;
            }
        }

        private void LinkAfter(Event e, Event? prev) => Link(e, prev, prev != null ? prev.Next : head);
        private void LinkBefore(Event e, Event? next) => Link(e, next != null ? next.Prev : tail, next);

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
                prev = prev.Prev;
            }
            LinkAfter(e, prev);

            e.State = Event.EventState.Scheduled;
            e.Time = time;
        }

        public void MoveEvent(ulong timeDiff, Event e)
        {
            if (e == null || e.State != Event.EventState.Scheduled)
            {
                return;
            }

            var time = CurrentTime + timeDiff;
            if (time < e.Time)
            {
                var prev = e.Prev;
                while (prev != null && prev.Time > time)
                {
                    prev = prev.Prev;
                }
                if (prev != e.Prev)
                {
                    Unlink(e);
                    LinkAfter(e, prev);
                }
            }
            else if (time > e.Time)
            {
                var next = e.Next;
                while (next != null && next.Time <= time)
                {
                    next = next.Next;
                }
                if (next != e.Next)
                {
                    Unlink(e);
                    LinkBefore(e, next);
                }
            }

            e.Time = time;
        }

        public void RemoveEvent(Event e)
        {
            if (e.State != Event.EventState.Scheduled)
            {
                return;
            }

            Unlink(e);
            e.State = Event.EventState.Cancelled;
        }

        public void ExecuteUntil(ulong endTime)
        {
            while (head != null && head.Time < endTime)
            {
                Event e = head;
                Unlink(e);
                CurrentTime = e.Time;
                e.State = Event.EventState.Executed;
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

    sealed class TestSpell : Spell
    {
        public TestSpell(Sim sim) : base("Test Spell", sim, 1500, 100) { }
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
            // TODO: add some buffs here
        }

        public struct SpellList
        {
            public Spell TestSpell;
            // TODO: add more spells here
        }

        private struct SimState
        {
            public Event? ReadyEvent;
            public Event? CastEvent;
            public Spell? Casting;
            public ulong GcdReady;
        }

        public EventQueue Events = new EventQueue();
        public Random Rng = new Random();
        public Statistics Stats = new Statistics();
        public Logger Log;

        public ulong CurrentTime => Events.CurrentTime;

        public BuffList Buffs;
        public SpellList Spells;
        private SimState state;

        public Sim(bool log = false)
        {
            Log = new Logger(this, log);

            // TODO: create new buffs and spells here
            Spells.TestSpell = new TestSpell(this);
        }

        private void BeginCast()
        {
            state.ReadyEvent = null;
            // TODO: add some way to select spells here
            Spell spell = Spells.TestSpell;
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
            Events.Reset();
            Stats.Reset();

            // TODO: reset any newly added buffs and spells here
            Spells.TestSpell.Reset();

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
            Sim s = new Sim(true);
            s.Run(100_000);
        }
    }
}

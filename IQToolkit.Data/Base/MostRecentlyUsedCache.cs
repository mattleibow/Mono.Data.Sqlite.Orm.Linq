using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Diagnostics;
using System.Threading;
using System.Runtime.CompilerServices;

namespace IQToolkit
{
	/// <summary>
	/// Implements a cache over a most recently used list
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class MostRecentlyUsedCache<T>
	{
		private int maxSize;

		private List<T> list;

		private Func<T, T, bool> fnEquals;

		private ReaderWriterLockSlim rwlock;

		private int version;

		public MostRecentlyUsedCache(int maxSize)
			: this(maxSize, EqualityComparer<T>.Default)
		{
		}

		public MostRecentlyUsedCache(int maxSize, IEqualityComparer<T> comparer)
			: this(maxSize, (x, y) => comparer.Equals(x, y))
		{
		}

		public MostRecentlyUsedCache(int maxSize, Func<T, T, bool> fnEquals)
		{
			this.list = new List<T>();
			this.maxSize = maxSize;
			this.fnEquals = fnEquals;
			this.rwlock = new ReaderWriterLockSlim();
		}

		public int Count
		{
			get
			{
				this.rwlock.EnterReadLock();
				try
				{
					return this.list.Count;
				}
				finally
				{
					this.rwlock.ExitReadLock();
				}
			}
		}

		public void Clear()
		{
			this.rwlock.EnterWriteLock();
			try
			{
				this.list.Clear();
				this.version++;
			}
			finally
			{
				this.rwlock.ExitWriteLock();
			}
		}

		public bool Lookup(T item, bool add, out T cachedItem)
		{
			cachedItem = default(T);

			rwlock.EnterReadLock();
			int cacheIndex = -1;
			int version = this.version;
			try
			{
				FindInList_NoLock(item, out cacheIndex, out cachedItem);
			}
			finally
			{
				rwlock.ExitReadLock();
			}

			if (cacheIndex == -1 || add)
			{
				rwlock.EnterWriteLock();
				try
				{
					// if list has changed find it again
					if (this.version != version)
					{
						FindInList_NoLock(item, out cacheIndex, out cachedItem);
					}

					if (cacheIndex == -1)
					{
						// this is first time in list, put at start
						this.list.Insert(0, item);
						cachedItem = item;
						cacheIndex = 0;
					}
					else if (cacheIndex > 0)
					{
						// if item is not at start, move it to the start
						this.list.RemoveAt(cacheIndex);
						this.list.Insert(0, item);
						cacheIndex = 0;
					}

					// drop any items beyond max
					if (this.list.Count > this.maxSize)
					{
						this.list.RemoveAt(this.list.Count - 1);
					}

					this.version++;
				}
				finally
				{
					rwlock.ExitWriteLock();
				}
			}

			return cacheIndex >= 0;
		}

		private void FindInList_NoLock(T item, out int index, out T cached)
		{
			index = -1;
			cached = default(T);

			for (int i = 0, n = this.list.Count; i < n; i++)
			{
				cached = this.list[i];
				if (fnEquals(cached, item))
				{
					index = i;
					break;
				}
			}
		}
	}
}

namespace System.Threading {
		[Flags]
		internal enum LockState
		{
			None = 0,
			Read = 1,
			Write = 2,
			Upgradable = 4,
			UpgradedRead = Upgradable | Read,
			UpgradedWrite = Upgradable | Write
		}

		internal class ThreadLockState
		{
			public LockState LockState;
			public int ReaderRecursiveCount;
			public int WriterRecursiveCount;
			public int UpgradeableRecursiveCount;
		}

		[Serializable]
		public enum LockRecursionPolicy
		{
			NoRecursion,
			SupportsRecursion
		}

	public class ReaderWriterLockSlim : IDisposable
	{
		/* Position of each bit isn't really important 
		 * but their relative order is
		 */
		const int RwReadBit = 3;

		const int RwWait = 1;
		const int RwWaitUpgrade = 2;
		const int RwWrite = 4;
		const int RwRead = 8;

		int rwlock;

		readonly LockRecursionPolicy recursionPolicy;

		AtomicBoolean upgradableTaken = new AtomicBoolean ();
		ManualResetEventSlim upgradableEvent = new ManualResetEventSlim (true);
		ManualResetEventSlim writerDoneEvent = new ManualResetEventSlim (true);
		ManualResetEventSlim readerDoneEvent = new ManualResetEventSlim (true);

		int numReadWaiters, numUpgradeWaiters, numWriteWaiters;
		bool disposed;

		static int idPool = int.MinValue;
		readonly int id = Interlocked.Increment (ref idPool);

		[ThreadStatic]
		static IDictionary<int, ThreadLockState> currentThreadState;

		public ReaderWriterLockSlim () : this (LockRecursionPolicy.NoRecursion)
		{
		}

		public ReaderWriterLockSlim (LockRecursionPolicy recursionPolicy)
		{
			this.recursionPolicy = recursionPolicy;
		}

		public void EnterReadLock ()
		{
			TryEnterReadLock (-1);
		}

		public bool TryEnterReadLock (int millisecondsTimeout)
		{
			ThreadLockState ctstate = CurrentThreadState;

			if (CheckState (millisecondsTimeout, LockState.Read)) {
				ctstate.ReaderRecursiveCount++;
				return true;
			}

			// This is downgrading from upgradable, no need for check since
			// we already have a sort-of read lock that's going to disappear
			// after user calls ExitUpgradeableReadLock.
			// Same idea when recursion is allowed and a write thread wants to
			// go for a Read too.
			if (CurrentLockState.HasFlag (LockState.Upgradable)
			    || recursionPolicy == LockRecursionPolicy.SupportsRecursion) {
				Interlocked.Add (ref rwlock, RwRead);
				ctstate.LockState ^= LockState.Read;
				ctstate.ReaderRecursiveCount++;

				return true;
			}

			Stopwatch sw = Stopwatch.StartNew ();
			Interlocked.Increment (ref numReadWaiters);

			while (millisecondsTimeout == -1 || sw.ElapsedMilliseconds < millisecondsTimeout) {
				if ((rwlock & 0x7) > 0) {
					writerDoneEvent.Wait (ComputeTimeout (millisecondsTimeout, sw));
					continue;
				}

				if ((Interlocked.Add (ref rwlock, RwRead) & 0x7) == 0) {
					ctstate.LockState ^= LockState.Read;
					ctstate.ReaderRecursiveCount++;
					Interlocked.Decrement (ref numReadWaiters);
					if (readerDoneEvent.IsSet )
						readerDoneEvent.Reset ();
					return true;
				}

				Interlocked.Add (ref rwlock, -RwRead);

				writerDoneEvent.Wait (ComputeTimeout (millisecondsTimeout, sw));
			}

			Interlocked.Decrement (ref numReadWaiters);
			return false;
		}

		public bool TryEnterReadLock (TimeSpan timeout)
		{
			return TryEnterReadLock (CheckTimeout (timeout));
		}

		public void ExitReadLock ()
		{
			ThreadLockState ctstate = CurrentThreadState;

			if (!ctstate.LockState.HasFlag (LockState.Read))
				throw new Exception ("The current thread has not entered the lock in read mode");

			ctstate.LockState ^= LockState.Read;
			ctstate.ReaderRecursiveCount--;
			if (Interlocked.Add (ref rwlock, -RwRead) >> RwReadBit == 0)
				readerDoneEvent.Set ();
		}

		public void EnterWriteLock ()
		{
			TryEnterWriteLock (-1);
		}

		public bool TryEnterWriteLock (int millisecondsTimeout)
		{
			ThreadLockState ctstate = CurrentThreadState;

			if (CheckState (millisecondsTimeout, LockState.Write)) {
				ctstate.WriterRecursiveCount++;
				return true;
			}

			Stopwatch sw = Stopwatch.StartNew ();
			Interlocked.Increment (ref numWriteWaiters);
			bool isUpgradable = ctstate.LockState.HasFlag (LockState.Upgradable);

			// If the code goes there that means we had a read lock beforehand
			if (isUpgradable && rwlock >= RwRead)
				Interlocked.Add (ref rwlock, -RwRead);

			int stateCheck = isUpgradable ? RwWaitUpgrade : RwWait;
			int appendValue = RwWait | (isUpgradable ? RwWaitUpgrade : 0);

			while (millisecondsTimeout < 0 || sw.ElapsedMilliseconds < millisecondsTimeout) {
				int state = rwlock;

				if (state <= stateCheck) {
					if (Interlocked.CompareExchange (ref rwlock, RwWrite, state) == state) {
						ctstate.LockState ^= LockState.Write;
						ctstate.WriterRecursiveCount++;
						Interlocked.Decrement (ref numWriteWaiters);
						if (writerDoneEvent.IsSet )
							writerDoneEvent.Reset ();
						return true;
					}
					state = rwlock;
				}

				while ((state & RwWait) == 0 && Interlocked.CompareExchange (ref rwlock, state | appendValue, state) == state)
					state = rwlock;

				while (rwlock > stateCheck && (millisecondsTimeout < 0 || sw.ElapsedMilliseconds < millisecondsTimeout))
					readerDoneEvent.Wait (ComputeTimeout (millisecondsTimeout, sw));
			}

			Interlocked.Decrement (ref numWriteWaiters);
			return false;
		}

		public bool TryEnterWriteLock (TimeSpan timeout)
		{
			return TryEnterWriteLock (CheckTimeout (timeout));
		}

		public void ExitWriteLock ()
		{
			ThreadLockState ctstate = CurrentThreadState;

			if (!ctstate.LockState.HasFlag (LockState.Write))
				throw new Exception("The current thread has not entered the lock in write mode");

			ctstate.LockState ^= LockState.Write;
			ctstate.WriterRecursiveCount--;
			Interlocked.Add (ref rwlock, -RwWrite);
			writerDoneEvent.Set ();
		}

		public void EnterUpgradeableReadLock ()
		{
			TryEnterUpgradeableReadLock (-1);
		}

		//
		// Taking the Upgradable read lock is like taking a read lock
		// but we limit it to a single upgradable at a time.
		//
		public bool TryEnterUpgradeableReadLock (int millisecondsTimeout)
		{
			ThreadLockState ctstate = CurrentThreadState;

			if (CheckState (millisecondsTimeout, LockState.Upgradable)) {
				ctstate.UpgradeableRecursiveCount++;
				return true;
			}

			if (ctstate.LockState.HasFlag(LockState.Read))
				throw new Exception("The current thread has already entered read mode");

			Stopwatch sw = Stopwatch.StartNew ();
			Interlocked.Increment (ref numUpgradeWaiters);

			while (!upgradableEvent.IsSet  || !upgradableTaken.TryRelaxedSet ()) {
				if (millisecondsTimeout != -1 && sw.ElapsedMilliseconds > millisecondsTimeout) {
					Interlocked.Decrement (ref numUpgradeWaiters);
					return false;
				}

				upgradableEvent.Wait (ComputeTimeout (millisecondsTimeout, sw));
			}

			upgradableEvent.Reset ();

			if (TryEnterReadLock (ComputeTimeout (millisecondsTimeout, sw))) {
				ctstate.LockState = LockState.Upgradable;
				Interlocked.Decrement (ref numUpgradeWaiters);
				ctstate.ReaderRecursiveCount--;
				ctstate.UpgradeableRecursiveCount++;
				return true;
			}

			upgradableTaken.Value = false;
			upgradableEvent.Set ();

			Interlocked.Decrement (ref numUpgradeWaiters);

			return false;
		}

		public bool TryEnterUpgradeableReadLock (TimeSpan timeout)
		{
			return TryEnterUpgradeableReadLock (CheckTimeout (timeout));
		}

		public void ExitUpgradeableReadLock ()
		{
			ThreadLockState ctstate = CurrentThreadState;

			if (!ctstate.LockState.HasFlag (LockState.Upgradable | LockState.Read))
				throw new Exception("The current thread has not entered the lock in upgradable mode");

			upgradableTaken.Value = false;
			upgradableEvent.Set ();

			ctstate.LockState ^= LockState.Upgradable;
			ctstate.UpgradeableRecursiveCount--;
			if (Interlocked.Add (ref rwlock, -RwRead) >> RwReadBit == 0)
				readerDoneEvent.Set ();
		}

		public void Dispose ()
		{
			disposed = true;
		}

		public bool IsReadLockHeld {
			get {
				return rwlock >= RwRead;
			}
		}

		public bool IsWriteLockHeld {
			get {
				return (rwlock & RwWrite) > 0;
			}
		}

		public bool IsUpgradeableReadLockHeld {
			get {
				return upgradableTaken.Value;
			}
		}

		public int CurrentReadCount {
			get {
				return (rwlock >> RwReadBit) - (IsUpgradeableReadLockHeld ? 1 : 0);
			}
		}

		public int RecursiveReadCount {
			get {
				return CurrentThreadState.ReaderRecursiveCount;
			}
		}

		public int RecursiveUpgradeCount {
			get {
				return CurrentThreadState.UpgradeableRecursiveCount;
			}
		}

		public int RecursiveWriteCount {
			get {
				return CurrentThreadState.WriterRecursiveCount;
			}
		}

		public int WaitingReadCount {
			get {
				return numReadWaiters;
			}
		}

		public int WaitingUpgradeCount {
			get {
				return numUpgradeWaiters;
			}
		}

		public int WaitingWriteCount {
			get {
				return numWriteWaiters;
			}
		}

		public LockRecursionPolicy RecursionPolicy {
			get {
				return recursionPolicy;
			}
		}

		LockState CurrentLockState {
			get {
				return CurrentThreadState.LockState;
			}
			set {
				CurrentThreadState.LockState = value;
			}
		}

		ThreadLockState CurrentThreadState {
			get {
				if (currentThreadState == null)
					currentThreadState = new Dictionary<int, ThreadLockState> ();

				ThreadLockState state;
				if (!currentThreadState.TryGetValue (id, out state))
					currentThreadState[id] = state = new ThreadLockState ();

				return state;
			}
		}

		bool CheckState (int millisecondsTimeout, LockState validState)
		{
			if (disposed)
				throw new ObjectDisposedException ("ReaderWriterLockSlim");

			if (millisecondsTimeout < Timeout.Infinite)
				throw new ArgumentOutOfRangeException ("millisecondsTimeout");

			// Detect and prevent recursion
			LockState ctstate = CurrentLockState;

			if (recursionPolicy == LockRecursionPolicy.NoRecursion)
				if ((ctstate != LockState.None && ctstate != LockState.Upgradable)
				    || (ctstate == LockState.Upgradable && validState == LockState.Upgradable))
					throw new Exception("The current thread has already a lock and recursion isn't supported");

			// If we already had right lock state, just return
			if (ctstate.HasFlag (validState))
				return true;

			CheckRecursionAuthorization (ctstate, validState);

			return false;
		}

		static void CheckRecursionAuthorization (LockState ctstate, LockState desiredState)
		{
			// In read mode you can just enter Read recursively
			if (ctstate == LockState.Read)
				throw new Exception();				
		}

		static int CheckTimeout (TimeSpan timeout)
		{
			try {
				return checked ((int)timeout.TotalMilliseconds);
			} catch (System.OverflowException) {
				throw new ArgumentOutOfRangeException ("timeout");
			}
		}

		static int ComputeTimeout (int millisecondsTimeout, Stopwatch sw)
		{
			return millisecondsTimeout == -1 ? -1 : (int)Math.Max (sw.ElapsedMilliseconds - millisecondsTimeout, 1);
		}
	}

	internal class AtomicBoolean
	{
		int flag;
		const int UnSet = 0;
		const int Set = 1;

		public bool CompareAndExchange(bool expected, bool newVal)
		{
			int newTemp = newVal ? Set : UnSet;
			int expectedTemp = expected ? Set : UnSet;

			return Interlocked.CompareExchange(ref flag, newTemp, expectedTemp) == expectedTemp;
		}

		public static AtomicBoolean FromValue(bool value)
		{
			AtomicBoolean temp = new AtomicBoolean();
			temp.Value = value;

			return temp;
		}

		public bool TrySet()
		{
			return !Exchange(true);
		}

		public bool TryRelaxedSet()
		{
			return flag == UnSet && !Exchange(true);
		}

		public bool Exchange(bool newVal)
		{
			int newTemp = newVal ? Set : UnSet;
			return Interlocked.Exchange(ref flag, newTemp) == Set;
		}

		public bool Value
		{
			get
			{
				return flag == Set;
			}
			set
			{
				Exchange(value);
			}
		}

		public bool Equals(AtomicBoolean rhs)
		{
			return this.flag == rhs.flag;
		}

		public override bool Equals(object rhs)
		{
			return rhs is AtomicBoolean ? Equals((AtomicBoolean)rhs) : false;
		}

		public override int GetHashCode()
		{
			return flag.GetHashCode();
		}

		public static explicit operator bool(AtomicBoolean rhs)
		{
			return rhs.Value;
		}

		public static implicit operator AtomicBoolean(bool rhs)
		{
			return AtomicBoolean.FromValue(rhs);
		}
	}

	public class Stopwatch
	{
		[MethodImpl(MethodImplOptions.InternalCall)]
		public static extern long GetTimestamp();

		public static readonly long Frequency = 10000000;

		public static readonly bool IsHighResolution = true;

		public static Stopwatch StartNew()
		{
			Stopwatch s = new Stopwatch();
			s.Start();
			return s;
		}

		public Stopwatch()
		{
		}

		long elapsed;
		long started;
		bool is_running;

		public TimeSpan Elapsed
		{
			get
			{
				if (IsHighResolution)
				{
					// convert our ticks to TimeSpace ticks, 100 nano second units
					// using two divisions helps avoid overflow
					return TimeSpan.FromTicks((long)(ElapsedTicks / (Frequency / TimeSpan.TicksPerSecond)));
				}
				else
				{
					return TimeSpan.FromTicks(ElapsedTicks);
				}
			}
		}

		public long ElapsedMilliseconds
		{
			get
			{
				checked
				{
					if (IsHighResolution)
					{
						return (long)(ElapsedTicks / (Frequency / 1000));
					}
					else
					{
						return (long)Elapsed.TotalMilliseconds;
					}
				}
			}
		}

		public long ElapsedTicks
		{
			get { return is_running ? GetTimestamp() - started + elapsed : elapsed; }
		}

		public bool IsRunning
		{
			get { return is_running; }
		}

		public void Reset()
		{
			elapsed = 0;
			is_running = false;
		}

		public void Start()
		{
			if (is_running)
				return;
			started = GetTimestamp();
			is_running = true;
		}

		public void Stop()
		{
			if (!is_running)
				return;
			elapsed += GetTimestamp() - started;
			is_running = false;
		}

		public void Restart ()
		{
			started = GetTimestamp ();
			elapsed = 0;
			is_running = true;
		}
	}
}
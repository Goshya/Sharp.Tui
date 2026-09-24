namespace Stopwatch;

// Elapsed only grows while Running; LastTick is the time of the previous tick, so each tick adds
// exactly the time that passed since it. Update stays pure: it never reads the clock itself.
internal sealed record Model(TimeSpan Elapsed, bool Running, DateTimeOffset? LastTick);

using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using NUnit.Framework;

namespace Content.Tests.Shared._Shitmed.Surgery;

/// <summary>
/// The contract the grey-vs-hide surgery UI rests on: a hard filter cancels (hidden), a fixable
/// prerequisite adds a reason without cancelling (greyed), and either way the surgery is Blocked so
/// <c>IsSurgeryValid</c> keeps it un-performable.
/// </summary>
[TestFixture]
[TestOf(typeof(SurgeryValidEvent))]
public sealed class SurgeryValidEventTest
{
    [Test]
    public void FreshEventBlocksNothing()
    {
        var ev = new SurgeryValidEvent(default, default);

        Assert.That(ev.Blocked, Is.False);
        Assert.That(ev.BlockReasons, Is.Null);
    }

    [Test]
    public void HardCancelBlocksWithoutReasons()
    {
        var ev = new SurgeryValidEvent(default, default) { Cancelled = true };

        Assert.Multiple(() =>
        {
            Assert.That(ev.Blocked, Is.True);
            // No reasons -> the UI hides it rather than greying it.
            Assert.That(ev.BlockReasons, Is.Null);
        });
    }

    [Test]
    public void SoftBlockGreysButStaysBlocked()
    {
        var ev = new SurgeryValidEvent(default, default);
        ev.AddBlockReason("surgery-blocked-bone");

        Assert.Multiple(() =>
        {
            // Not hard-cancelled -> the UI greys the surgery instead of hiding it.
            Assert.That(ev.Cancelled, Is.False);
            // But still Blocked -> IsSurgeryValid keeps it un-performable until the prerequisite is fixed.
            Assert.That(ev.Blocked, Is.True);
            Assert.That(ev.BlockReasons, Does.Contain("surgery-blocked-bone"));
        });
    }
}

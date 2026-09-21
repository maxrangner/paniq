namespace Paniq.Simulation
{
    /// <summary>
    /// Something a frightened person might do instead of simply running: fetch
    /// an extinguisher, see to somebody who has collapsed, hit an alarm, wedge
    /// a door, follow whoever is taking charge.
    ///
    /// Each one is asked in turn and the first that answers wins, so the order
    /// they are registered in is the order of priority. That order used to be a
    /// chain of if-statements inside the panic rules, with the reasoning for
    /// each step buried beside it; it is now one list in one place, which is
    /// also the only edit needed to add another thing people might do.
    /// </summary>
    internal interface IPanicOption
    {
        /// <summary>
        /// What this person would rather be doing than running, or null to
        /// leave the decision to whatever comes next.
        /// </summary>
        MotorIntent? Decide(Agent agent, bool inDanger);
    }
}

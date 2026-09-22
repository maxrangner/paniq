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
        /// <param name="eager">
        /// Set on a way out they can see standing open, right now. Only
        /// <see cref="BarricadeBehaviour"/> reads this today, to stop somebody
        /// starting to wedge themselves in while a clear escape stands open
        /// (though they still finish one already under way).
        /// </param>
        MotorIntent? Decide(Agent agent, bool inDanger, bool eager);
    }
}

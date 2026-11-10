using System;
using Momo.Core;
using UnityEngine;

namespace Momo
{
    public static class PetPresentationPath
    {
        // Render a point along the current route between discrete simulation ticks.
        // This does not change WorldState or the save file.
        public static Vector3 Target(WorldState state)
        {
            Point point = state.pet;
            if (state.activity == Activity.Walk && state.route.Count > 0)
                point = Point.Toward(point, state.route[0],
                    (float)Math.Min(state.remainder, Simulation.TickSeconds) * .18f);
            return new Vector3(point.x, 0, point.z);
        }
    }
}

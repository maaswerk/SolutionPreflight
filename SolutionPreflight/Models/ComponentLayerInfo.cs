using System;

namespace SolutionPreflight.Models
{
    /// <summary>
    /// Projection of a `msdyn_componentlayer` record for display in the Solution Layers grid.
    /// </summary>
    public class ComponentLayerInfo
    {
        public Guid ComponentLayerId { get; set; }

        public Guid ComponentId { get; set; }

        /// <summary>
        /// Numeric solution component type (see `solutioncomponent`/`msdyn_componentlayer` "componenttype" choice).
        /// </summary>
        public int ComponentType { get; set; }

        public string ComponentTypeName { get; set; }

        public string ComponentName { get; set; }

        public string SolutionName { get; set; }

        public string PublisherName { get; set; }

        public bool IsUnmanaged { get; set; }

        public DateTime? EndTime { get; set; }

        /// <summary>
        /// True when <see cref="ComponentType"/> is in the whitelist that
        /// <see cref="Layers.SolutionLayerService"/> knows how to remove via
        /// the `RemoveActiveCustomization` action. Unsupported types must be
        /// removed manually in the maker portal.
        /// </summary>
        public bool CanRemove { get; set; }

        public bool Selected { get; set; }
    }
}

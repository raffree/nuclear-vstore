using System;

namespace CloningTool.Json
{
    public class PlacementDescriptor : IEquatable<PlacementDescriptor>
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public override string ToString() => $"{Id} - {Name}";

        public bool Equals(PlacementDescriptor other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Id == other.Id && string.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((PlacementDescriptor)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ (Name?.GetHashCode() ?? 0);
            }
        }
    }
}

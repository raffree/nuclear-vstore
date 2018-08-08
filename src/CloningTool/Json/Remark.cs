using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CloningTool.Json
{
    public class Remark : IEquatable<Remark>
    {
        public long Id { get; set; }
        public JObject Name { get; set; }
        public RemarkCategory Category { get; set; }
        public List<long> Placements { get; set; }
        public List<long> Countries { get; set; }
        public bool IsHidden { get; set; }
        public JObject Description { get; set; }
        public JObject ModeratorDescription { get; set; }
        public RemarkApplicability Applicability { get; set; }

        /// <inheritdoc />
        public bool Equals(Remark other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Id == other.Id &&
                   IsHidden == other.IsHidden &&
                   Applicability == other.Applicability &&
                   Category?.Id == other.Category?.Id &&
                   new HashSet<long>(Countries).SetEquals(other.Countries) &&
                   new HashSet<long>(Placements).SetEquals(other.Placements) &&
                   JToken.DeepEquals(Name, other.Name) &&
                   JToken.DeepEquals(Description, other.Description) &&
                   JToken.DeepEquals(ModeratorDescription, other.ModeratorDescription);
        }

        /// <inheritdoc />
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

            return obj.GetType() == GetType() && Equals((Remark)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id.GetHashCode();
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Category != null ? Category.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Placements != null ? Placements.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Countries != null ? Countries.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsHidden.GetHashCode();
                hashCode = (hashCode * 397) ^ (Description != null ? Description.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ModeratorDescription != null ? ModeratorDescription.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)Applicability;
                return hashCode;
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"{Id} - {Name}";
    }
}
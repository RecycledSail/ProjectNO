

using System.Collections.Generic;

public class NamedPerson
{
    public int id { get; }
    public string firstName { get; }
    public string lastName { get; }

    public Species species { get; }
    public List<Trait> traits { get; set; }

    public NamedPerson(int id, string firstName, string lastName, Species species)
    {
        this.id = id;
        this.firstName = firstName;
        this.lastName = lastName;
        this.species = species;
        this.traits = new();
    }

    public bool AddTrait(Trait trait)
    {
        if (traits.Contains(trait))
        {
            return false;
        }
        else
        {
            traits.Add(trait);
            return true;
        }
    }

    public bool RemoveTrait(Trait trait)
    {
        return traits.Remove(trait);
    }

    public string FullName()
    {
        return firstName + " " + lastName;
    }
}
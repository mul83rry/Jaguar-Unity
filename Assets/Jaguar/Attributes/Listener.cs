using System;

namespace Jaguar.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ListenerAttribute : Attribute
    {
        public string Name { get; set; }

        public ListenerAttribute(string name) => Name = name;

        public ListenerAttribute() { }
    }
}
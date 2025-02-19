﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Max2Babylon
{
    public class AnimationGroup
    {
        public bool IsDirty { get; private set; } = true;

        public Guid SerializedId
        {
            get { return serializedId; }
            set
            {
                if (value.Equals(SerializedId))
                    return;
                IsDirty = true;
                serializedId = value;
            }
        }
        public string Name
        {
            get { return name; }
            set
            {
                if (value.Equals(name))
                    return;
                IsDirty = true;
                name = value;
            }
        }
        public int FrameStart
        {
            get { return Tools.RoundToInt(ticksStart / (float)Loader.Global.TicksPerFrame); }
            set
            {
                if (value.Equals(FrameStart)) // property getter
                    return;
                IsDirty = true;
                ticksStart = value * Loader.Global.TicksPerFrame;
            }
        }
        public int FrameEnd
        {
            get { return Tools.RoundToInt(ticksEnd / (float)Loader.Global.TicksPerFrame); }
            set
            {
                if (value.Equals(FrameEnd)) // property getter
                    return;
                IsDirty = true;
                ticksEnd = value * Loader.Global.TicksPerFrame;
            }
        }
        public IList<uint> NodeHandles
        {
            get { return nodeHandles.AsReadOnly(); }
            set
            {
                // if the lists are equal, return early so isdirty is not touched
                if (nodeHandles.Count == value.Count)
                {
                    bool equal = true;
                    int i = 0;
                    foreach(uint newNodeHandle in value)
                    {
                        if(!newNodeHandle.Equals(nodeHandles[i]))
                        {
                            equal = false;
                            break;
                        }
                        ++i;
                    }
                    if (equal)
                        return;
                }

                IsDirty = true;
                nodeHandles.Clear();
                nodeHandles.AddRange(value);
            }
        }

        public int TicksStart { get { return ticksStart; } }
        public int TicksEnd { get { return ticksEnd; } }

        const string s_DisplayNameFormat = "{0} ({1:d}, {2:d})";
        const char s_PropertySeparator = ';';
        const string s_PropertyFormat = "{0};{1};{2};{3}";

        private Guid serializedId = Guid.NewGuid();
        private string name = "Animation";
        // use current timeline frame range by default
        private int ticksStart = Loader.Core.AnimRange.Start;
        private int ticksEnd = Loader.Core.AnimRange.End;
        private List<uint> nodeHandles = new List<uint>();

        public AnimationGroup() { }
        public AnimationGroup(AnimationGroup other)
        {
            DeepCopyFrom(other);
        }
        public void DeepCopyFrom(AnimationGroup other)
        {
            serializedId = other.serializedId;
            name = other.name;
            ticksStart = other.ticksStart;
            ticksEnd = other.ticksEnd;
            nodeHandles.Clear();
            nodeHandles.AddRange(other.nodeHandles);
            IsDirty = true;
        }

        public override string ToString()
        {
            return string.Format(s_DisplayNameFormat, name, FrameStart, FrameEnd);
        }

        #region Serialization

        public string GetPropertyName() { return serializedId.ToString(); }

        public void LoadFromData(string propertyName)
        {
            if (!Guid.TryParse(propertyName, out serializedId))
                throw new Exception("Invalid ID, can't deserialize.");

            string propertiesString = string.Empty;
            if (!Loader.Core.RootNode.GetUserPropString(propertyName, ref propertiesString))
                return;

            string[] properties = propertiesString.Split(s_PropertySeparator);

            if (properties.Length < 4)
                throw new Exception("Invalid number of properties, can't deserialize.");

            // set dirty explicitly just before we start loading, set to false when loading is done
            // if any exception is thrown, it will have a correct value
            IsDirty = true;

            name = properties[0];
            if (!int.TryParse(properties[1], out ticksStart))
                throw new Exception("Failed to parse FrameStart property.");
            if (!int.TryParse(properties[2], out ticksEnd))
                throw new Exception("Failed to parse FrameEnd property.");

            if (string.IsNullOrEmpty(properties[3]))
                return;

            int numNodeIDs = properties.Length - 3;
            if (nodeHandles.Capacity < numNodeIDs) nodeHandles.Capacity = numNodeIDs;
            int numFailed = 0;
            for (int i = 0; i < numNodeIDs; ++i)
            {
                uint id;
                if (!uint.TryParse(properties[3 + i], out id))
                {
                    ++numFailed;
                    continue;
                }
                nodeHandles.Add(id);
            }

            if (numFailed > 0)
                throw new Exception(string.Format("Failed to parse {0} node ids.", numFailed));
            
            IsDirty = false;
        }

        public void SaveToData()
        {
            // ' ' and '=' are not allowed by max, ';' is our data separator
            if (name.Contains(' ') || name.Contains('=') || name.Contains(s_PropertySeparator))
                throw new FormatException("Invalid character(s) in animation Name: " + name + ". Spaces, equal signs and the separator '" + s_PropertySeparator + "' are not allowed.");

            string nodes = string.Join(s_PropertySeparator.ToString(), nodeHandles);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendFormat(s_PropertyFormat, name, ticksStart, ticksEnd, nodes);

            Loader.Core.RootNode.SetStringProperty(GetPropertyName(), stringBuilder.ToString());

            IsDirty = false;
        }

        public void DeleteFromData()
        {
            Loader.Core.RootNode.DeleteProperty(GetPropertyName());
            IsDirty = true;
        }

        #endregion
    }

    public class AnimationGroupList : List<AnimationGroup>
    {
        const string s_AnimationListPropertyName = "babylonjs_AnimationList";

        public void LoadFromData()
        {
            string[] animationPropertyNames = Loader.Core.RootNode.GetStringArrayProperty(s_AnimationListPropertyName);

            if (Capacity < animationPropertyNames.Length)
                Capacity = animationPropertyNames.Length;

            foreach (string propertyNameStr in animationPropertyNames)
            {
                AnimationGroup info = new AnimationGroup();
                info.LoadFromData(propertyNameStr);
                Add(info);
            }
        }

        public void SaveToData()
        {
            List<string> animationPropertyNameList = new List<string>();
            for(int i = 0; i < Count; ++i)
            {
                if(this[i].IsDirty)
                    this[i].SaveToData();
                animationPropertyNameList.Add(this[i].GetPropertyName());
            }
            
            Loader.Core.RootNode.SetStringArrayProperty(s_AnimationListPropertyName, animationPropertyNameList);
        }
    }
}

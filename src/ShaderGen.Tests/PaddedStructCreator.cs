﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ShaderGen.Tests
{
    /// <summary>
    /// This class is used to build a set of fields for use in testing methods.
    /// </summary>
    /// <seealso cref="IEnumerable{KeyValuePair{Type, string}}" />
    internal class PaddedStructCreator
    {
        internal class Field
        {
            public readonly string Name;
            public readonly Type Type;
            public readonly int Position;
            public readonly AlignmentInfo AlignmentInfo;
            public readonly bool IsPaddingField;

            /// <summary>
            /// Initializes a new instance of the <see cref="Field" /> class.
            /// </summary>
            /// <param name="name">The name.</param>
            /// <param name="type">The type.</param>
            /// <param name="position">The position.</param>
            /// <param name="alignmentInfo">The alignment information.</param>
            /// <param name="isPaddingField">if set to <c>true</c> this is a padding field.</param>
            public Field(string name, Type type, int position, AlignmentInfo alignmentInfo, bool isPaddingField = false)
            {
                Name = name;
                Position = position;
                Type = type;
                AlignmentInfo = alignmentInfo;
                IsPaddingField = isPaddingField;
            }
        }

        /// <summary>
        /// Holds fields of a specific type.
        /// </summary>
        private class Creator : IEnumerable<string>
        {
            private int _current = 0;
            public readonly Type Type;
            public readonly AlignmentInfo AlignmentInfo;
            private readonly List<string> _names = new List<string>(6);

            public Creator(Type type, AlignmentInfo alignmentInfo)
            {
                Type = type;
                AlignmentInfo = alignmentInfo;
            }

            /// <summary>
            /// Gets a field of this type.
            /// </summary>
            /// <returns></returns>
            public string GetFieldName()
            {
                if (_current < _names.Count)
                {
                    return _names[_current++];
                }

                string newName;

                _current++;
                _names.Add(newName = $"{Type.Name}_{_current}");
                return newName;
            }

            /// <summary>
            /// Resets the creator for this type.
            /// </summary>
            public void Reset() => _current = 0;

            /// <summary>
            /// Returns an enumerator that iterates through the collection.
            /// </summary>
            /// <returns>
            /// An enumerator that can be used to iterate through the collection.
            /// </returns>
            public IEnumerator<string> GetEnumerator() => _names.GetEnumerator();

            /// <summary>
            /// Returns an enumerator that iterates through a collection.
            /// </summary>
            /// <returns>
            /// An <see cref="T:System.Collections.IEnumerator"></see> object that can be used to iterate through the collection.
            /// </returns>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public override string ToString() => $"{_names.Count} fields of type {Type}";
        }

        /// <summary>
        /// All the creators by type.
        /// </summary>
        private readonly Dictionary<Type, Creator> _creators = new Dictionary<Type, Creator>();

        private readonly Compilation _compilation;

        public PaddedStructCreator(Compilation compilation) => _compilation = compilation;

        /// <summary>
        /// Gets a field of the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public string GetFieldName(Type type)
        {
            if (!_creators.TryGetValue(type, out Creator creator))
            {
                _creators.Add(type,
                    creator = new Creator(type,
                        TypeSizeCache.Get(_compilation.GetTypeByMetadataName(type.FullName))));
            }

            return creator.GetFieldName();
        }

        /// <summary>
        /// Resets the field creator.
        /// </summary>
        public void Reset()
        {
            foreach (Creator creator in _creators.Values)
            {
                creator.Reset();
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IReadOnlyList<Field> GetFields(out int size)
        {
            AlignmentInfo floatAlignmentInfo = TypeSizeCache.Get(_compilation.GetTypeByMetadataName(typeof(float).FullName));
            int paddingFields = 0;
            size = 0;

            // Create list of fields ordered by largest size first, and get the field names.
            List<(Creator creator, List<string> names)> fieldsBySize = _creators.Values
                .OrderByDescending(c => c.AlignmentInfo.ShaderAlignment)
                .ThenByDescending(c => c.AlignmentInfo.ShaderSize)
                .Select<Creator, (Creator creator, List<string> names)>(c => (c, c.ToList()))
                .Where(t => t.names.Count > 0)
                .ToList();

            // Output list of fields
            List<Field> fields = new List<Field>();

            // For as long as we have fields to place we loop.
            while (fieldsBySize.Count > 0)
            {
                // Get the top of the list
                (Creator creator, List<string> names) = fieldsBySize[0];
                fieldsBySize.RemoveAt(0);
                int alignment = creator.AlignmentInfo.ShaderAlignment;
                Assert.True(alignment % 4 == 0);

                foreach (string fieldName in names)
                {
                    // Check to see if we are aligned
                    while (size % alignment != 0)
                    {
                        Assert.True(size % 4 == 0);

                        // Do we have any fields we can use to pad?
                        int currentSize = size;
                        (Creator creator, List<string> names) padFields =
                            fieldsBySize.FirstOrDefault(t =>
                                t.creator.AlignmentInfo.ShaderSize <= alignment &&
                                currentSize % t.creator.AlignmentInfo.ShaderAlignment == 0);

                        if (padFields.creator != null)
                        {
                            // Use the last field to pad the struct.
                            fields.Add(new Field(padFields.names.Last(), padFields.creator.Type, size,
                                padFields.creator.AlignmentInfo));

                            // Increase the struct size.
                            size += padFields.creator.AlignmentInfo.ShaderSize;

                            // Remove the used field
                            padFields.names.RemoveAt(padFields.names.Count - 1);
                            if (padFields.names.Count < 1)
                            {
                                fieldsBySize.Remove(padFields);
                            }
                        }
                        else
                        {
                            // No padding field of the right size available, use a private float
                            fields.Add(new Field($"_paddingField_{paddingFields++}", typeof(float), size, floatAlignmentInfo, true));
                            size += floatAlignmentInfo.ShaderSize;
                        }
                    }

                    // Add the next field as we are on the correct alignment boundary
                    fields.Add(new Field(fieldName, creator.Type, size,
                        creator.AlignmentInfo));

                    size += creator.AlignmentInfo.ShaderSize;
                }
            }

            return fields;
        }
    }
}
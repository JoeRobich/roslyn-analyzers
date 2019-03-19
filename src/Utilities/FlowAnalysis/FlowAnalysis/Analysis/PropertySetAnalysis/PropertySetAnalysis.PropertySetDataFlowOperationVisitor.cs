﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer.Utilities.FlowAnalysis.Analysis.PropertySetAnalysis
{
    using PropertySetAnalysisData = DictionaryAnalysisData<AbstractLocation, PropertySetAbstractValue>;

    internal partial class PropertySetAnalysis
    {
        /// <summary>
        /// Operation visitor to flow the location validation values across a given statement in a basic block.
        /// </summary>
        private sealed class PropertySetDataFlowOperationVisitor :
            AbstractLocationDataFlowOperationVisitor<PropertySetAnalysisData, PropertySetAnalysisContext, PropertySetAnalysisResult, PropertySetAbstractValue>
        {
            private readonly ImmutableDictionary<(Location Location, IMethodSymbol Method), HazardousUsageEvaluationResult>.Builder _hazardousUsageBuilder;

            /// <summary>
            /// The type containing the property set we're tracking.
            /// </summary>
            private readonly INamedTypeSymbol TrackedTypeSymbol;

            public PropertySetDataFlowOperationVisitor(PropertySetAnalysisContext analysisContext)
                : base(analysisContext)
            {
                Debug.Assert(analysisContext.PointsToAnalysisResultOpt != null);

                _hazardousUsageBuilder = ImmutableDictionary.CreateBuilder<(Location Location, IMethodSymbol Method), HazardousUsageEvaluationResult>();

                this.WellKnownTypeProvider.TryGetTypeByMetadataName(analysisContext.TypeToTrackMetadataName, out this.TrackedTypeSymbol);
            }

            public override int GetHashCode()
            {
                return HashUtilities.Combine(_hazardousUsageBuilder.GetHashCode(), base.GetHashCode());
            }

            public ImmutableDictionary<(Location Location, IMethodSymbol Method), HazardousUsageEvaluationResult> HazardousUsages
            {
                get
                {
                    return _hazardousUsageBuilder.ToImmutable();
                }
            }

            protected override PropertySetAbstractValue GetAbstractDefaultValue(ITypeSymbol type) => ValueDomain.Bottom;

            protected override bool HasAnyAbstractValue(PropertySetAnalysisData data) => data.Count > 0;

            protected override PropertySetAbstractValue GetAbstractValue(AbstractLocation location)
                => this.CurrentAnalysisData.TryGetValue(location, out var value) ? value : ValueDomain.Bottom;

            protected override void ResetCurrentAnalysisData() => ResetAnalysisData(CurrentAnalysisData);

            protected override void StopTrackingAbstractValue(AbstractLocation location) => CurrentAnalysisData.Remove(location);

            protected override void SetAbstractValue(AbstractLocation location, PropertySetAbstractValue value)
            {
                if (value != PropertySetAbstractValue.Unknown
                    || this.CurrentAnalysisData.ContainsKey(location))
                {
                    this.CurrentAnalysisData[location] = value;
                }
            }

            protected override PropertySetAnalysisData MergeAnalysisData(PropertySetAnalysisData value1, PropertySetAnalysisData value2)
                => PropertySetAnalysisDomainInstance.Merge(value1, value2);
            protected override void UpdateValuesForAnalysisData(PropertySetAnalysisData targetAnalysisData)
                => UpdateValuesForAnalysisData(targetAnalysisData, CurrentAnalysisData);
            protected override PropertySetAnalysisData GetClonedAnalysisData(PropertySetAnalysisData analysisData)
                => GetClonedAnalysisDataHelper(analysisData);
            public override PropertySetAnalysisData GetEmptyAnalysisData()
                => GetEmptyAnalysisDataHelper();
            protected override PropertySetAnalysisData GetExitBlockOutputData(PropertySetAnalysisResult analysisResult)
                => GetClonedAnalysisDataHelper(analysisResult.ExitBlockOutput.Data);
            protected override void ApplyMissingCurrentAnalysisDataForUnhandledExceptionData(PropertySetAnalysisData dataAtException, ThrownExceptionInfo throwBranchWithExceptionType)
                => ApplyMissingCurrentAnalysisDataForUnhandledExceptionData(dataAtException, CurrentAnalysisData);
            protected override bool Equals(PropertySetAnalysisData value1, PropertySetAnalysisData value2)
                => EqualsHelper(value1, value2);

            protected override void SetValueForParameterPointsToLocationOnEntry(IParameterSymbol parameter, PointsToAbstractValue pointsToAbstractValue)
            {
            }

            protected override void EscapeValueForParameterPointsToLocationOnExit(IParameterSymbol parameter, AnalysisEntity analysisEntity, ImmutableHashSet<AbstractLocation> escapedLocations)
            {
            }

            protected override void SetAbstractValueForArrayElementInitializer(IArrayCreationOperation arrayCreation, ImmutableArray<AbstractIndex> indices, ITypeSymbol elementType, IOperation initializer, PropertySetAbstractValue value)
            {
            }

            protected override void SetAbstractValueForAssignment(IOperation target, IOperation assignedValueOperation, PropertySetAbstractValue assignedValue, bool mayBeAssignment = false)
            {
            }

            protected override void SetAbstractValueForTupleElementAssignment(AnalysisEntity tupleElementEntity, IOperation assignedValueOperation, PropertySetAbstractValue assignedValue)
            {
            }

            public override PropertySetAbstractValue VisitObjectCreation(IObjectCreationOperation operation, object argument)
            {
                PropertySetAbstractValue abstractValue = base.VisitObjectCreation(operation, argument);
                if (operation.Type != this.TrackedTypeSymbol)
                {
                    return abstractValue;
                }

                ConstructorMapper constructorMapper = this.DataFlowAnalysisContext.ConstructorMapper;
                if (!constructorMapper.PropertyAbstractValues.IsEmpty)
                {
                    abstractValue = PropertySetAbstractValue.GetInstance(constructorMapper.PropertyAbstractValues);
                }
                else if (constructorMapper.MapFromNullAbstractValue != null)
                {
                    ArrayBuilder<NullAbstractValue> builder = ArrayBuilder<NullAbstractValue>.GetInstance();
                    try
                    {
                        foreach (IArgumentOperation argumentOperation in operation.Arguments)
                        {
                            builder.Add(this.GetNullAbstractValue(argumentOperation));
                        }

                        abstractValue = constructorMapper.MapFromNullAbstractValue(operation.Constructor, builder);
                    }
                    finally
                    {
                        builder.Free();
                    }
                }
                else if (constructorMapper.MapFromValueContentAbstractValue != null)
                {
                    Debug.Assert(this.DataFlowAnalysisContext.ValueContentAnalysisResultOpt != null);
                    ArrayBuilder<NullAbstractValue> nullBuilder = ArrayBuilder<NullAbstractValue>.GetInstance();
                    ArrayBuilder<ValueContentAbstractValue> valueContentBuilder = ArrayBuilder<ValueContentAbstractValue>.GetInstance();
                    try
                    {
                        foreach (IArgumentOperation argumentOperation in operation.Arguments)
                        {
                            nullBuilder.Add(this.GetNullAbstractValue(argumentOperation));
                            valueContentBuilder.Add(this.GetValueContentAbstractValue(argumentOperation));
                        }

                        abstractValue = constructorMapper.MapFromValueContentAbstractValue(operation.Constructor, valueContentBuilder, nullBuilder);
                    }
                    finally
                    {
                        nullBuilder.Free();
                        valueContentBuilder.Free();
                    }
                }
                else
                {
                    Debug.Fail("Unhandled ConstructorMapper");
                    return abstractValue;
                }

                PointsToAbstractValue pointsToAbstractValue = this.GetPointsToAbstractValue(operation);
                this.SetAbstractValue(pointsToAbstractValue, abstractValue);
                return abstractValue;
            }

            protected override PropertySetAbstractValue VisitAssignmentOperation(IAssignmentOperation operation, object argument)
            {
                PropertySetAbstractValue baseValue = base.VisitAssignmentOperation(operation, argument);
                if (operation.Target is IPropertyReferenceOperation propertyReferenceOperation
                    && propertyReferenceOperation.Instance?.Type == this.TrackedTypeSymbol
                    && this.DataFlowAnalysisContext.PropertyMappers.TryGetPropertyMapper(
                        propertyReferenceOperation.Property.Name,
                        out PropertyMapper propertyMapper,
                        out int index))
                {
                    PropertySetAbstractValueKind propertySetAbstractValueKind;

                    if (propertyMapper.MapFromNullAbstractValue != null)
                    {
                        propertySetAbstractValueKind = propertyMapper.MapFromNullAbstractValue(
                            this.GetNullAbstractValue(operation.Value));
                    }
                    else if (propertyMapper.MapFromValueContentAbstractValue != null)
                    {
                        Debug.Assert(this.DataFlowAnalysisContext.ValueContentAnalysisResultOpt != null);
                        propertySetAbstractValueKind = propertyMapper.MapFromValueContentAbstractValue(
                            this.GetValueContentAbstractValue(operation.Value));
                    }
                    else
                    {
                        Debug.Fail("Unhandled PropertyMapper");
                        return baseValue;
                    }

                    baseValue = null;
                    PointsToAbstractValue pointsToAbstractValue = this.GetPointsToAbstractValue(propertyReferenceOperation.Instance);
                    foreach (AbstractLocation location in pointsToAbstractValue.Locations)
                    {
                        PropertySetAbstractValue propertySetAbstractValue = this.GetAbstractValue(location);
                        propertySetAbstractValue = propertySetAbstractValue.ReplaceAt(index, propertySetAbstractValueKind);

                        if (baseValue == null)
                        {
                            baseValue = propertySetAbstractValue;
                        }
                        else
                        {
                            baseValue = this.DataFlowAnalysisContext.ValueDomain.Merge(baseValue, propertySetAbstractValue);
                        }

                        this.SetAbstractValue(location, propertySetAbstractValue);
                    }

                    return baseValue ?? PropertySetAbstractValue.Unknown.ReplaceAt(index, propertySetAbstractValueKind);
                }

                return baseValue;
            }

            public override PropertySetAbstractValue VisitInvocation_NonLambdaOrDelegateOrLocalFunction(IMethodSymbol method, IOperation visitedInstance, ImmutableArray<IArgumentOperation> visitedArguments, bool invokedAsDelegate, IOperation originalOperation, PropertySetAbstractValue defaultValue)
            {
                PropertySetAbstractValue baseValue = base.VisitInvocation_NonLambdaOrDelegateOrLocalFunction(method, visitedInstance, visitedArguments, invokedAsDelegate, originalOperation, defaultValue);

                // If we have a HazardousUsageEvaluator for a method within the tracked type,
                // or for a method within a different type.
                IOperation propertySetInstance = visitedInstance;
                if ((visitedInstance?.Type == this.TrackedTypeSymbol
                    && this.DataFlowAnalysisContext.HazardousUsageEvaluators.TryGetHazardousUsageEvaluator(method.MetadataName, out var hazardousUsageEvaluator))
                    || TryFindNonTrackedTypeHazardousUsageEvaluator(out hazardousUsageEvaluator, out propertySetInstance))
                {
                    PointsToAbstractValue pointsToAbstractValue = this.GetPointsToAbstractValue(propertySetInstance);
                    bool hasFlagged = false;
                    bool hasMaybeFlagged = false;
                    foreach (AbstractLocation location in pointsToAbstractValue.Locations)
                    {
                        PropertySetAbstractValue locationAbstractValue = this.GetAbstractValue(location);

                        HazardousUsageEvaluationResult evaluationResult = hazardousUsageEvaluator.Evaluator(method, locationAbstractValue);
                        if (evaluationResult == HazardousUsageEvaluationResult.Flagged)
                        {
                            hasFlagged = true;
                        }
                        else if (evaluationResult == HazardousUsageEvaluationResult.MaybeFlagged)
                        {
                            hasMaybeFlagged = true;
                        }
                    }

                    (Location, IMethodSymbol) key = (originalOperation.Syntax.GetLocation(), method);
                    if (hasFlagged && !hasMaybeFlagged)
                    {
                        this._hazardousUsageBuilder.Add(key, HazardousUsageEvaluationResult.Flagged);
                    }
                    else if ((hasFlagged || hasMaybeFlagged)
                        && !this._hazardousUsageBuilder.ContainsKey(key))   // Keep existing value, if there is one.
                    {
                        this._hazardousUsageBuilder.Add(key, HazardousUsageEvaluationResult.MaybeFlagged);
                    }
                }

                return baseValue;

                // Local functions.
                bool TryFindNonTrackedTypeHazardousUsageEvaluator(out HazardousUsageEvaluator evaluator, out IOperation instance)
                {
                    evaluator = null;
                    instance = null;
                    if (!this.DataFlowAnalysisContext.HazardousUsageTypesToNames.TryGetValue(
                            visitedInstance?.Type as INamedTypeSymbol ?? method.ContainingType,
                            out string containingTypeName))
                    {
                        return false;
                    }

                    // This doesn't handle the case of multiple instances of the type being tracked.
                    // If that's needed one day, will need to extend this.
                    foreach (IArgumentOperation argumentOperation in visitedArguments)
                    {
                        if (argumentOperation.Value?.Type == this.TrackedTypeSymbol
                            && this.DataFlowAnalysisContext.HazardousUsageEvaluators.TryGetHazardousUsageEvaluator(
                                    containingTypeName,
                                    method.MetadataName,
                                    argumentOperation.Parameter.MetadataName,
                                    out evaluator))
                        {
                            instance = argumentOperation.Value;
                            return true;
                        }
                    }

                    return false;
                }
            }

            private ValueContentAbstractValue GetValueContentAbstractValue(IOperation operation)
            {
                Debug.Assert(
                    this.DataFlowAnalysisContext.ValueContentAnalysisResultOpt != null,
                    "PropertySetAnalysis should have computed ValueContentAnalysisResult if attempting to access it");
                return this.DataFlowAnalysisContext.ValueContentAnalysisResultOpt[operation];
            }
        }
    }
}

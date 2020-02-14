using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OpenRasta.Collections;

namespace OpenRasta
{
  public class UriTemplateTable
  {
    readonly List<KeyValuePair<UriTemplate, object>> _keyValuePairs;
    ReadOnlyCollection<KeyValuePair<UriTemplate, object>> _keyValuePairsReadOnly;

    public UriTemplateTable() : this(null, null)
    {
    }

    public UriTemplateTable(IEnumerable<KeyValuePair<UriTemplate, object>> keyValuePairs)
      : this(null, keyValuePairs)
    {
    }

    public UriTemplateTable(Uri baseAddress)
      : this(baseAddress, null)
    {
    }

    public UriTemplateTable(Uri baseAddress, IEnumerable<KeyValuePair<UriTemplate, object>> keyValuePairs)
    {
      BaseAddress = baseAddress;
      _keyValuePairs = keyValuePairs != null
        ? new List<KeyValuePair<UriTemplate, object>>(keyValuePairs)
        : new List<KeyValuePair<UriTemplate, object>>();
    }

    public Uri BaseAddress { get; set; }

    public bool IsReadOnly { get; private set; }

    public IList<KeyValuePair<UriTemplate, object>> KeyValuePairs => IsReadOnly
      ? _keyValuePairsReadOnly
      : (IList<KeyValuePair<UriTemplate, object>>) _keyValuePairs;

    /// <exception cref="InvalidOperationException">You need to set a BaseAddress before calling MakeReadOnly</exception>
    public void MakeReadOnly(bool allowDuplicateEquivalentUriTemplates)
    {
      if (BaseAddress == null)
        throw new InvalidOperationException("You need to set a BaseAddress before calling MakeReadOnly");
      if (!allowDuplicateEquivalentUriTemplates)
        EnsureAllTemplatesAreDifferent();
      IsReadOnly = true;
      _keyValuePairsReadOnly = _keyValuePairs.AsReadOnly();
    }
    public Collection<UriTemplateMatch> Match(Uri uri)
    {
      var lastMaxLiteralSegmentCount = 0;
      var matches = new Collection<UriTemplateMatch>();
      if (BaseAddress != null)
      {
        var baseLeft = BaseAddress.GetLeftPart(UriPartial.Authority);
        var baseSegments = BaseAddress.Segments;
        foreach (var template in KeyValuePairs)
        {
          // TODO: discard uri templates with fragment identifiers until tests are implemented
          if (template.Key.Fragment.Any()) continue;

          UriTemplateMatch potentialMatch = template.Key.Match(BaseAddress,baseLeft, baseSegments, uri);

          if (potentialMatch == null) continue;

          // this calculates and keep only what matches the maximum possible amount of literal segments
          var currentMaxLiteralSegmentCount = potentialMatch.RelativePathSegments.Count
                                              - potentialMatch.WildcardPathSegments.Count;
          for (var i = 0; i < potentialMatch.PathSegmentVariables.Count; i++)
            if (potentialMatch.QueryParameters == null ||
                potentialMatch.QueryStringVariables[potentialMatch.PathSegmentVariables.GetKey(i)] == null)
              currentMaxLiteralSegmentCount -= 1;

          potentialMatch.Data = template.Value;

          if (currentMaxLiteralSegmentCount > lastMaxLiteralSegmentCount)
          {
            lastMaxLiteralSegmentCount = currentMaxLiteralSegmentCount;
          }
          else if (currentMaxLiteralSegmentCount < lastMaxLiteralSegmentCount)
          {
            continue;
          }

          matches.Add(potentialMatch);
        }
      }

      return SortByMatchQuality(matches).ToCollection();
    }

    IEnumerable<UriTemplateMatch> SortByMatchQuality(Collection<UriTemplateMatch> matches)
    {
      return from m in matches
        let missingQueryStringParameters = Math.Abs(m.QueryStringVariables.Count - m.QueryParameters.Count)
        let matchedVariables = m.PathSegmentVariables.Count + m.QueryStringVariables.Count
        let literalSegments = m.RelativePathSegments.Count - m.PathSegmentVariables.Count
        orderby literalSegments descending, matchedVariables descending, missingQueryStringParameters
        select m;
    }

    /// <exception cref="UriTemplateMatchException">Several matching templates were found.</exception>
    public UriTemplateMatch MatchSingle(Uri uri)
    {
      UriTemplateMatch singleMatch = null;
      if (BaseAddress != null)
      {
        var baseLeft = BaseAddress.GetLeftPart(UriPartial.Authority);
        var baseSegments = BaseAddress.Segments;
        foreach (var segmentKey in KeyValuePairs)
        {
          UriTemplateMatch potentialMatch = segmentKey.Key.Match(BaseAddress,baseLeft,baseSegments,uri);
          if (potentialMatch != null && singleMatch != null)
            throw new UriTemplateMatchException("Several matching templates were found.");
          if (potentialMatch != null)
          {
            singleMatch = potentialMatch;
            singleMatch.Data = segmentKey.Value;
          }
        }
      }

      return singleMatch;
    }

    /// <exception cref="InvalidOperationException">Two equivalent templates were found.</exception>
    void EnsureAllTemplatesAreDifferent()
    {
      // highly unoptimized, but good enough for now. It's an O(n!) in all cases
      // if you want to implement a sort algorithm on this, be my guest. It's only called
      // once per application lifecycle so not sure there's much value.
      for (int i = 0; i < _keyValuePairs.Count; i++)
      {
        KeyValuePair<UriTemplate, object> rootKey = _keyValuePairs[i];
        for (int j = i + 1; j < _keyValuePairs.Count; j++)
          if (rootKey.Key.IsEquivalentTo(_keyValuePairs[j].Key))
            throw new InvalidOperationException("Two equivalent templates were found.");
      }
    }
  }
}
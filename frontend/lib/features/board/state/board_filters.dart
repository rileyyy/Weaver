import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/shared/models/tag_match.dart';
import 'package:weaver/shared/models/work_item_query.dart';

/// Everything the board's header controls: time range, search, hidden
/// status columns, selected tags and sort. Immutable; each change returns a
/// new value.
///
/// Which items match the time range, search and tags is the server's
/// decision (ADR-0006): [toQuery] says what to ask for. Hidden statuses and
/// sort only change how the result is laid out, so they stay here.
class BoardFilters {
  const BoardFilters({
    this.start,
    this.end,
    this.searchQuery = '',
    this.hiddenStatusIds = const {},
    this.selectedTags = const {},
    this.tagMatch = TagMatch.any,
    this.sortOption = CardSortOption.manual,
  });

  final DateTime? start;
  final DateTime? end;
  final String searchQuery;
  final Set<String> hiddenStatusIds;

  /// Lower-cased, so "Urgent" and "urgent" are one selection, the same way
  /// the tag chip list merges them.
  final Set<String> selectedTags;
  final TagMatch tagMatch;
  final CardSortOption sortOption;

  /// Either bound may be null (open on that side). An inverted range is
  /// swapped rather than silently matching nothing; the pickers prevent
  /// one anyway.
  BoardFilters withTimeRange({DateTime? start, DateTime? end}) {
    final inverted = start != null && end != null && start.isAfter(end);
    return _copy(start: inverted ? end : start, end: inverted ? start : end);
  }

  BoardFilters withSearchQuery(String query) => _copy(searchQuery: query);

  BoardFilters withStatusToggled(String statusId) =>
      _copy(hiddenStatusIds: _toggled(hiddenStatusIds, statusId));

  BoardFilters withTagToggled(String tag) =>
      _copy(selectedTags: _toggled(selectedTags, tag.toLowerCase()));

  BoardFilters withTagMatch(TagMatch match) => _copy(tagMatch: match);

  BoardFilters withSortOption(CardSortOption option) =>
      _copy(sortOption: option);

  bool isTagSelected(String tag) => selectedTags.contains(tag.toLowerCase());

  /// The server query for [spaceId] with this header's time range, search
  /// and tags. Hiding a status or changing the sort leaves it equal, as
  /// neither needs a new request.
  WorkItemQuery toQuery(String spaceId) => WorkItemQuery(
    spaceId: spaceId,
    text: searchQuery,
    tags: selectedTags,
    tagMatch: tagMatch,
    from: start,
    to: end,
  );

  static Set<String> _toggled(Set<String> set, String value) =>
      Set.unmodifiable(
        set.contains(value) ? ({...set}..remove(value)) : {...set, value},
      );

  BoardFilters _copy({
    Object? start = _unchanged,
    Object? end = _unchanged,
    String? searchQuery,
    Set<String>? hiddenStatusIds,
    Set<String>? selectedTags,
    TagMatch? tagMatch,
    CardSortOption? sortOption,
  }) => BoardFilters(
    start: identical(start, _unchanged) ? this.start : start as DateTime?,
    end: identical(end, _unchanged) ? this.end : end as DateTime?,
    searchQuery: searchQuery ?? this.searchQuery,
    hiddenStatusIds: hiddenStatusIds ?? this.hiddenStatusIds,
    selectedTags: selectedTags ?? this.selectedTags,
    tagMatch: tagMatch ?? this.tagMatch,
    sortOption: sortOption ?? this.sortOption,
  );

  static const Object _unchanged = Object();
}

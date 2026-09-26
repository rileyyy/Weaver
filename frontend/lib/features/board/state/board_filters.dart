import 'package:weaver/core/dates/calendar_days.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

/// Everything the board's header controls: time range, search, hidden
/// status columns, selected tags and sort. Immutable; each change returns a
/// new value. All filtering is client-side over data already loaded.
class BoardFilters {
  const BoardFilters({
    this.start,
    this.end,
    this.searchQuery = '',
    this.hiddenStatusIds = const {},
    this.selectedTags = const {},
    this.sortOption = CardSortOption.manual,
  });

  final DateTime? start;
  final DateTime? end;
  final String searchQuery;
  final Set<String> hiddenStatusIds;

  /// Lower-cased: tags match case-insensitively, the same way the tag chip
  /// list merges "Urgent" and "urgent".
  final Set<String> selectedTags;
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

  BoardFilters withSortOption(CardSortOption option) =>
      _copy(sortOption: option);

  bool isTagSelected(String tag) => selectedTags.contains(tag.toLowerCase());

  /// Interval overlap, not containment: a missing bound on either side (the
  /// item's or the filter's) is open-ended. Compared by calendar day, so
  /// both bounds include their whole day.
  bool matchesTimeWindow(DateTime? itemStart, DateTime? itemEnd) {
    final filterStart = start;
    final filterEnd = end;
    final startsInTime =
        filterEnd == null ||
        itemStart == null ||
        daysBetween(filterEnd, itemStart) <= 0;
    final endsInTime =
        filterStart == null ||
        itemEnd == null ||
        daysBetween(filterStart, itemEnd) >= 0;
    return startsInTime && endsInTime;
  }

  /// Title, description or any tag contains the query, ignoring case.
  bool matchesSearchText(String title, String? description, List<String> tags) {
    final query = searchQuery.trim().toLowerCase();
    if (query.isEmpty) return true;
    return title.toLowerCase().contains(query) ||
        (description?.toLowerCase().contains(query) ?? false) ||
        tags.any((tag) => tag.toLowerCase().contains(query));
  }

  /// Any selected tag matches (OR); no selection applies no tag filter.
  bool matchesTags(List<String> tags) =>
      selectedTags.isEmpty ||
      tags.any((tag) => selectedTags.contains(tag.toLowerCase()));

  /// Hidden statuses aren't checked here: the board only iterates visible
  /// columns, so a card in a hidden one is never asked about.
  bool cardVisible(WorkItemCard card) =>
      matchesTimeWindow(card.startDate, card.endDate) &&
      matchesSearchText(card.title, card.description, card.tags) &&
      matchesTags(card.tags);

  /// The Hierarchy isn't organised by column, so it checks hidden statuses
  /// itself.
  bool hierarchyItemVisible(HierarchyItem item) =>
      !hiddenStatusIds.contains(item.statusId) &&
      matchesTimeWindow(item.startDate, item.endDate) &&
      matchesSearchText(item.title, item.description, item.tags) &&
      matchesTags(item.tags);

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
    CardSortOption? sortOption,
  }) => BoardFilters(
    start: identical(start, _unchanged) ? this.start : start as DateTime?,
    end: identical(end, _unchanged) ? this.end : end as DateTime?,
    searchQuery: searchQuery ?? this.searchQuery,
    hiddenStatusIds: hiddenStatusIds ?? this.hiddenStatusIds,
    selectedTags: selectedTags ?? this.selectedTags,
    sortOption: sortOption ?? this.sortOption,
  );

  static const Object _unchanged = Object();
}

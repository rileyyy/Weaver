import 'package:weaver/features/board/models/card_sort_option.dart';

/// The comparator for [option] over any work-item shape, or null for
/// [CardSortOption.manual]: items keep the backend's rank order, and the
/// caller must skip sorting entirely because Dart's `List.sort` isn't
/// stable (an always-0 comparator could reshuffle it). Sorting is
/// display-only and never changes `Rank`.
Comparator<T>? sortComparator<T>(
  CardSortOption option, {
  required String Function(T item) title,
  required DateTime? Function(T item) startDate,
  required DateTime? Function(T item) endDate,
}) => switch (option) {
  CardSortOption.manual => null,
  CardSortOption.title => (a, b) => title(
    a,
  ).toLowerCase().compareTo(title(b).toLowerCase()),
  CardSortOption.startDate => (a, b) => compareOpenEndedDates(
    startDate(a),
    startDate(b),
  ),
  CardSortOption.dueDate => (a, b) => compareOpenEndedDates(
    endDate(a),
    endDate(b),
  ),
};

/// Ascending, with a missing date after every present one: an unscheduled
/// item has no position to sort by, so it goes last rather than first.
int compareOpenEndedDates(DateTime? a, DateTime? b) {
  if (a == null) return b == null ? 0 : 1;
  if (b == null) return -1;
  return a.compareTo(b);
}

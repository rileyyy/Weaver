import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/state/work_item_sorting.dart';

typedef _Item = ({String title, DateTime? start, DateTime? end});

Comparator<_Item>? _comparator(CardSortOption option) => sortComparator<_Item>(
  option,
  title: (i) => i.title,
  startDate: (i) => i.start,
  endDate: (i) => i.end,
);

void main() {
  test('manual has no comparator, so the caller keeps rank order', () {
    expect(_comparator(CardSortOption.manual), isNull);
  });

  test('title sorts case-insensitively', () {
    final items = <_Item>[
      (title: 'beta', start: null, end: null),
      (title: 'Alpha', start: null, end: null),
    ]..sort(_comparator(CardSortOption.title));

    expect(items.map((i) => i.title), ['Alpha', 'beta']);
  });

  test('dates sort ascending with missing dates last', () {
    final items = <_Item>[
      (title: 'none', start: null, end: null),
      (title: 'late', start: DateTime(2026, 9, 30), end: null),
      (title: 'early', start: DateTime(2026, 9, 1), end: null),
    ]..sort(_comparator(CardSortOption.startDate));

    expect(items.map((i) => i.title), ['early', 'late', 'none']);
  });
}

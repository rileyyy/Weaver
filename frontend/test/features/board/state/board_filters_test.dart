import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/view_definition.dart';
import 'package:weaver/features/board/models/view_layout.dart';
import 'package:weaver/features/board/state/board_filters.dart';
import 'package:weaver/shared/models/tag_match.dart';

bool _asksDifferently(BoardFilters a, BoardFilters b) =>
    a.toQuery({'home'}) != b.toQuery({'home'});

void main() {
  const none = BoardFilters();

  test('by default the query hides old Done items', () {
    final query = none.toQuery({'home'});

    expect(query.applyDoneHiding, isTrue);
    expect(query.isUnfiltered, isFalse);
  });

  test('showing Done items with no other filter asks for every item', () {
    final query = none.withShowDone(true).toQuery({'home'});

    expect(query.applyDoneHiding, isFalse);
    expect(query.isUnfiltered, isTrue);
  });

  test('changes return new values and leave the original untouched', () {
    final filtered = none
        .withSearchQuery('x')
        .withStatusToggled('done')
        .withTagToggled('Urgent')
        .withTagMatch(TagMatch.all)
        .withSortOption(CardSortOption.title);

    expect(none.searchQuery, isEmpty);
    expect(none.hiddenStatusIds, isEmpty);
    expect(none.tagMatch, TagMatch.any);
    expect(filtered.hiddenStatusIds, {'done'});
    expect(filtered.isTagSelected('urgent'), isTrue);
    expect(filtered.tagMatch, TagMatch.all);
    expect(filtered.sortOption, CardSortOption.title);
  });

  test('toggling twice removes the entry again', () {
    expect(
      none.withStatusToggled('done').withStatusToggled('done').hiddenStatusIds,
      isEmpty,
    );
    expect(none.withTagToggled('a').withTagToggled('A').selectedTags, isEmpty);
  });

  test('the exposed sets are read-only', () {
    final filtered = none.withStatusToggled('done');

    expect(() => filtered.hiddenStatusIds.add('todo'), throwsUnsupportedError);
  });

  test('clearing one bound keeps the other', () {
    final filter = none
        .withTimeRange(start: DateTime(2026, 9, 1), end: DateTime(2026, 9, 30))
        .withTimeRange(start: DateTime(2026, 9, 1));

    expect(filter.start, DateTime(2026, 9, 1));
    expect(filter.end, isNull);
  });

  test('an inverted range is swapped, since the server rejects one', () {
    final filter = none.withTimeRange(
      start: DateTime(2026, 9, 30),
      end: DateTime(2026, 9, 1),
    );

    expect(filter.start, DateTime(2026, 9, 1));
    expect(filter.end, DateTime(2026, 9, 30));
  });

  test('the query carries the trimmed search, tags and time range', () {
    final query = none
        .withSearchQuery('  design ')
        .withTagToggled('Urgent')
        .withTimeRange(start: DateTime(2026, 9, 1), end: DateTime(2026, 9, 30))
        .toQuery({'home'});

    expect(query.spaceIds, {'home'});
    expect(query.text, 'design');
    expect(query.tags, {'urgent'});
    expect(query.from, DateTime(2026, 9, 1));
    expect(query.to, DateTime(2026, 9, 30));
    expect(query.isUnfiltered, isFalse);
  });

  test('a blank search alone adds no filter', () {
    expect(
      none.withSearchQuery('   ').toQuery({'home'}),
      none.toQuery({'home'}),
    );
  });

  group('whether a change needs a new request', () {
    test('toggling Show done asks again', () {
      expect(_asksDifferently(none.withShowDone(true), none), isTrue);
    });

    test('hiding a status or changing the sort asks nothing new', () {
      expect(
        _asksDifferently(
          none.withStatusToggled('done').withSortOption(CardSortOption.title),
          none,
        ),
        isFalse,
      );
    });

    test('a change of search, tags or time range asks again', () {
      expect(_asksDifferently(none.withSearchQuery('x'), none), isTrue);
      expect(_asksDifferently(none.withTagToggled('x'), none), isTrue);
      expect(
        _asksDifferently(none.withTimeRange(start: DateTime(2026, 9, 1)), none),
        isTrue,
      );
    });

    test('only spaces around the search ask nothing new', () {
      final typed = none.withSearchQuery('x');

      expect(_asksDifferently(typed.withSearchQuery(' x '), typed), isFalse);
    });

    test('Any or All matters only with two or more tags', () {
      final one = none.withTagToggled('a');
      final two = one.withTagToggled('b');

      expect(_asksDifferently(one.withTagMatch(TagMatch.all), one), isFalse);
      expect(_asksDifferently(two.withTagMatch(TagMatch.all), two), isTrue);
    });
  });

  group('the Roadmap query', () {
    final november = (
      start: DateTime(2026, 11, 1),
      end: DateTime(2026, 11, 30),
    );

    test('asks for the visible window when the header has no range', () {
      final query = none.withSearchQuery('paint').toRoadmapQuery({
        'home',
      }, november)!;

      expect(query.from, DateTime(2026, 11, 1));
      expect(query.to, DateTime(2026, 11, 30));
      expect(query.text, 'paint');
      expect(query.applyDoneHiding, isTrue);
    });

    test('asks for only the days both the window and the header range '
        'cover', () {
      final query = none
          .withTimeRange(
            start: DateTime(2026, 11, 20),
            end: DateTime(2027, 1, 1),
          )
          .toRoadmapQuery({'home'}, november)!;

      expect(query.from, DateTime(2026, 11, 20));
      expect(query.to, DateTime(2026, 11, 30));
    });

    test('keeps the window where the header range is open', () {
      final query = none
          .withTimeRange(end: DateTime(2026, 11, 10))
          .toRoadmapQuery({'home'}, november)!;

      expect(query.from, DateTime(2026, 11, 1));
      expect(query.to, DateTime(2026, 11, 10));
    });

    test('is null when the header range is outside the window', () {
      final query = none
          .withTimeRange(start: DateTime(2026, 12, 1))
          .toRoadmapQuery({'home'}, november);

      expect(query, isNull);
    });
  });

  group('a View', () {
    const outside = ViewDefinition(
      name: 'Outside',
      spaceIds: {'home', 'village'},
      text: 'fence',
      tags: {'Outside'},
      tagMatch: TagMatch.all,
      applyDoneHiding: false,
      scheduledOrInProgressOnly: true,
    );

    test('replaces the query inputs it stores', () {
      final filters = none
          .withSearchQuery('paint')
          .withTagToggled('urgent')
          .withView(outside);

      expect(filters.spaceIds, {'home', 'village'});
      expect(filters.searchQuery, 'fence');
      expect(filters.isTagSelected('outside'), isTrue);
      expect(filters.isTagSelected('urgent'), isFalse);
      expect(filters.tagMatch, TagMatch.all);
      expect(filters.showDone, isTrue);
      expect(filters.scheduledOrInProgressOnly, isTrue);
    });

    test('keeps the time range, hidden statuses and sort, which it '
        'does not store', () {
      final filters = none
          .withTimeRange(start: DateTime(2026, 11, 1))
          .withStatusToggled('done')
          .withSortOption(CardSortOption.title)
          .withView(outside);

      expect(filters.start, DateTime(2026, 11, 1));
      expect(filters.hiddenStatusIds, {'done'});
      expect(filters.sortOption, CardSortOption.title);
    });

    test('is saved from the current inputs, with a name and layout', () {
      final definition = none
          .withView(outside)
          .withSearchQuery(' gate ')
          .toDefinition('Gates', ViewLayout.roadmap);

      expect(definition.name, 'Gates');
      expect(definition.spaceIds, {'home', 'village'});
      expect(definition.text, 'gate');
      expect(definition.tags, {'outside'});
      expect(definition.tagMatch, TagMatch.all);
      expect(definition.applyDoneHiding, isFalse);
      expect(definition.scheduledOrInProgressOnly, isTrue);
      expect(definition.layout, ViewLayout.roadmap);
    });

    test("asks for Overview's rule on the board", () {
      final query = none.withView(outside).toQuery({'home'});

      expect(query.scheduledOrInProgressOnly, isTrue);
      expect(query.isUnfiltered, isFalse);
    });

    test("leaves Overview's rule off the Roadmap, which only shows dated "
        'items', () {
      final query = none
          .withView(outside)
          .toRoadmapQuery(
            {'home'},
            (start: DateTime(2026, 11, 1), end: DateTime(2026, 11, 30)),
          )!;

      expect(query.scheduledOrInProgressOnly, isFalse);
    });
  });
}

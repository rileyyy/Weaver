import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/widgets/add_tag_button.dart';
import 'package:weaver/features/board/widgets/tag_badge.dart';
import 'package:weaver/features/board/widgets/tag_badge_row.dart';

Widget _wrap(double width, Widget child) => MaterialApp(
  home: Scaffold(
    body: SizedBox(width: width, child: child),
  ),
);

void main() {
  testWidgets('TagBadgeRow renders nothing for an empty tag list', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(400, const TagBadgeRow(tags: [])));

    expect(find.byType(TagBadge), findsNothing);
  });

  testWidgets('TagBadgeRow renders every badge when they all fit', (
    tester,
  ) async {
    await tester.pumpWidget(
      _wrap(400, const TagBadgeRow(tags: ['bug', 'urgent'])),
    );

    expect(find.text('bug'), findsOneWidget);
    expect(find.text('urgent'), findsOneWidget);
    expect(find.textContaining('+'), findsNothing);
  });

  testWidgets(
    'TagBadgeRow appends a "+N" overflow badge when tags do not all fit',
    (tester) async {
      await tester.pumpWidget(
        _wrap(
          60,
          const TagBadgeRow(
            tags: ['bug', 'urgent', 'needs review', 'design', 'backend'],
          ),
        ),
      );

      // Whatever fits in 60px, at least one tag must be hidden behind an
      // overflow badge rather than silently dropped or overflowing the row.
      expect(find.textContaining('+'), findsOneWidget);
    },
  );

  testWidgets(
    'TagBadge marks the overflow variant distinctly from a real tag',
    (tester) async {
      await tester.pumpWidget(
        _wrap(
          200,
          const Column(
            children: [
              TagBadge(label: 'bug'),
              TagBadge(label: '+2', isOverflow: true),
            ],
          ),
        ),
      );

      final realBadge = tester.widget<TagBadge>(
        find.widgetWithText(TagBadge, 'bug'),
      );
      final overflowBadge = tester.widget<TagBadge>(
        find.widgetWithText(TagBadge, '+2'),
      );
      expect(realBadge.isOverflow, isFalse);
      expect(overflowBadge.isOverflow, isTrue);
    },
  );

  testWidgets(
    'TagBadgeRow with onAddTag shows only the add button when there are no tags',
    (tester) async {
      await tester.pumpWidget(
        _wrap(400, TagBadgeRow(tags: const [], onAddTag: () {})),
      );

      expect(find.byType(AddTagButton), findsOneWidget);
      expect(find.byType(TagBadge), findsNothing);
    },
  );

  testWidgets('tapping the add button calls onAddTag', (tester) async {
    var taps = 0;
    await tester.pumpWidget(
      _wrap(400, TagBadgeRow(tags: const ['bug'], onAddTag: () => taps++)),
    );

    await tester.tap(find.byType(AddTagButton));

    expect(taps, 1);
    expect(find.text('bug'), findsOneWidget);
  });

  testWidgets('the add button keeps its place when the tags overflow', (
    tester,
  ) async {
    await tester.pumpWidget(
      _wrap(
        90,
        TagBadgeRow(
          tags: const ['bug', 'urgent', 'needs review', 'design', 'backend'],
          onAddTag: () {},
        ),
      ),
    );

    expect(find.byType(AddTagButton), findsOneWidget);
    expect(find.textContaining(RegExp(r'^\+\d+$')), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
    'a row too narrow for any badge but the add button does not overflow',
    (tester) async {
      await tester.pumpWidget(
        _wrap(24, TagBadgeRow(tags: const ['bug', 'urgent'], onAddTag: () {})),
      );

      // A RenderFlex overflow would surface here as an exception.
      expect(tester.takeException(), isNull);
      expect(find.byType(AddTagButton), findsOneWidget);
    },
  );
}

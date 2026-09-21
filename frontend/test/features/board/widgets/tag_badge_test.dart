import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/widgets/tag_badge.dart';

Widget _wrap(double width, Widget child) => MaterialApp(
      home: Scaffold(body: SizedBox(width: width, child: child)),
    );

void main() {
  testWidgets('TagBadgeRow renders nothing for an empty tag list', (tester) async {
    await tester.pumpWidget(_wrap(400, const TagBadgeRow(tags: [])));

    expect(find.byType(TagBadge), findsNothing);
  });

  testWidgets('TagBadgeRow renders every badge when they all fit', (tester) async {
    await tester.pumpWidget(_wrap(400, const TagBadgeRow(tags: ['bug', 'urgent'])));

    expect(find.text('bug'), findsOneWidget);
    expect(find.text('urgent'), findsOneWidget);
    expect(find.textContaining('+'), findsNothing);
  });

  testWidgets('TagBadgeRow appends a "+N" overflow badge when tags do not all fit', (tester) async {
    await tester.pumpWidget(_wrap(
      60,
      const TagBadgeRow(tags: ['bug', 'urgent', 'needs review', 'design', 'backend']),
    ));

    // Whatever fits in 60px, at least one tag must be hidden behind an
    // overflow badge rather than silently dropped or overflowing the row.
    expect(find.textContaining('+'), findsOneWidget);
  });

  testWidgets('TagBadge marks the overflow variant distinctly from a real tag', (tester) async {
    await tester.pumpWidget(_wrap(
      200,
      const Column(
        children: [
          TagBadge(label: 'bug'),
          TagBadge(label: '+2', isOverflow: true),
        ],
      ),
    ));

    final realBadge = tester.widget<TagBadge>(find.widgetWithText(TagBadge, 'bug'));
    final overflowBadge = tester.widget<TagBadge>(find.widgetWithText(TagBadge, '+2'));
    expect(realBadge.isOverflow, isFalse);
    expect(overflowBadge.isOverflow, isTrue);
  });
}

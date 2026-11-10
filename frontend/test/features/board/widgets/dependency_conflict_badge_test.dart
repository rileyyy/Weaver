import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/widgets/dependency_conflict_badge.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

WorkItemBlocker _blocker(
  String title, {
  required int number,
  required bool conflicts,
}) => WorkItemBlocker(
  id: title.toLowerCase(),
  number: number,
  title: title,
  spaceId: 'home',
  endDate: DateTime(2026, 11, 20),
  conflicts: conflicts,
);

Widget _badge(List<WorkItemBlocker> blockers, {VoidCallback? onTapBehind}) =>
    MaterialApp(
      home: Scaffold(
        body: Center(
          child: InkWell(
            onTap: onTapBehind,
            child: DependencyConflictBadge(blockers: blockers),
          ),
        ),
      ),
    );

void main() {
  testWidgets('shows nothing when no blocker conflicts', (tester) async {
    await tester.pumpWidget(
      _badge([_blocker('Drywall', number: 12, conflicts: false)]),
    );

    expect(find.byType(Icon), findsNothing);
    expect(find.byType(Tooltip), findsNothing);
  });

  testWidgets('shows nothing without blockers', (tester) async {
    await tester.pumpWidget(_badge(const []));

    expect(find.byType(Icon), findsNothing);
  });

  testWidgets('names only the conflicting blockers', (tester) async {
    await tester.pumpWidget(
      _badge([
        _blocker('Drywall', number: 12, conflicts: true),
        _blocker('Permit', number: 3, conflicts: false),
        _blocker('Wiring', number: 7, conflicts: true),
      ]),
    );

    final tooltip = tester.widget<Tooltip>(find.byType(Tooltip));
    expect(
      tooltip.message,
      'Starts before #12 Drywall ends\nStarts before #7 Wiring ends',
    );
  });

  testWidgets('uses the theme error colour', (tester) async {
    await tester.pumpWidget(
      _badge([_blocker('Drywall', number: 12, conflicts: true)]),
    );

    final context = tester.element(find.byType(DependencyConflictBadge));
    expect(
      tester.widget<Icon>(find.byType(Icon)).color,
      Theme.of(context).colorScheme.error,
    );
  });

  testWidgets(
    'a tap shows the tooltip without reaching what the badge sits on',
    (tester) async {
      var tapsBehind = 0;
      await tester.pumpWidget(
        _badge([
          _blocker('Drywall', number: 12, conflicts: true),
        ], onTapBehind: () => tapsBehind++),
      );

      await tester.tap(find.byType(DependencyConflictBadge));
      await tester.pump(const Duration(milliseconds: 500));

      expect(find.text('Starts before #12 Drywall ends'), findsOneWidget);
      expect(tapsBehind, 0);
      await tester.pump(const Duration(seconds: 2));
    },
  );
}

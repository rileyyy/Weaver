import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/views/header/widgets/breadcrumb_bar.dart';

Widget _bar({
  required List<ScopeCrumb> breadcrumbs,
  List<ScopeCrumb> drillDownTargets = const [],
  List<int>? selected,
  List<ScopeCrumb>? drilled,
}) => MaterialApp(
  home: Scaffold(
    body: BreadcrumbBar(
      breadcrumbs: breadcrumbs,
      onSelect: (index) async => selected?.add(index),
      drillDownTargets: drillDownTargets,
      onDrillDown: (target) async => drilled?.add(target),
    ),
  ),
);

void main() {
  const board = ScopeCrumb(id: null, title: 'Board');
  const project = ScopeCrumb(id: 'project', title: 'Project');
  const other = ScopeCrumb(id: 'other', title: 'Other');

  testWidgets('tapping the current crumb drills into the chosen lane', (
    tester,
  ) async {
    final drilled = <ScopeCrumb>[];
    await tester.pumpWidget(
      _bar(
        breadcrumbs: const [board],
        drillDownTargets: const [project, other],
        drilled: drilled,
      ),
    );

    await tester.tap(find.text('Board'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Other').last);
    await tester.pumpAndSettle();

    expect(drilled, [other]);
  });

  testWidgets('the current crumb offers no menu without lanes', (tester) async {
    await tester.pumpWidget(_bar(breadcrumbs: const [board]));

    expect(find.byType(PopupMenuButton<ScopeCrumb>), findsNothing);
    expect(find.text('Board'), findsOneWidget);
  });

  testWidgets('tapping an earlier crumb navigates back to it', (tester) async {
    final selected = <int>[];
    await tester.pumpWidget(
      _bar(
        breadcrumbs: const [board, project],
        drillDownTargets: const [other],
        selected: selected,
      ),
    );

    await tester.tap(find.text('Board'));
    await tester.pumpAndSettle();

    expect(selected, [0]);
  });
}

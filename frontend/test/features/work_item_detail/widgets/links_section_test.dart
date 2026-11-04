import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link_type.dart';
import 'package:weaver/features/work_item_detail/widgets/links_section.dart';

WorkItemLink _link(
  String id,
  WorkItemLinkType type, {
  required int number,
  required String title,
}) => WorkItemLink(
  id: id,
  type: type,
  linkedWorkItemId: 'item-$number',
  linkedWorkItemNumber: number,
  linkedWorkItemTitle: title,
  linkedWorkItemSpaceId: 'home',
);

Future<void> _pump(
  WidgetTester tester, {
  List<WorkItemLink> links = const [],
  ValueChanged<WorkItemLinkType>? onAdd,
  ValueChanged<String>? onRemove,
  ValueChanged<String>? onOpen,
}) => tester.pumpWidget(
  MaterialApp(
    home: Scaffold(
      body: LinksSection(
        links: links,
        onAdd: onAdd ?? (_) {},
        onRemove: onRemove ?? (_) {},
        onOpen: onOpen ?? (_) {},
      ),
    ),
  ),
);

void main() {
  testWidgets('says when there are no linked items', (tester) async {
    await _pump(tester);

    expect(find.text('No linked work items'), findsOneWidget);
  });

  testWidgets('groups links under their type, in a fixed order', (
    tester,
  ) async {
    await _pump(
      tester,
      links: [
        _link('a', WorkItemLinkType.related, number: 3, title: 'Lights'),
        _link('b', WorkItemLinkType.blockedBy, number: 4, title: 'Drywall'),
        _link('c', WorkItemLinkType.blocks, number: 5, title: 'Trim'),
      ],
    );

    expect(find.text('No linked work items'), findsNothing);
    final order = [
      'Blocks',
      '#5 Trim',
      'Blocked by',
      '#4 Drywall',
      'Related',
      '#3 Lights',
    ];
    final tops = [
      for (final text in order) tester.getTopLeft(find.text(text).first).dy,
    ];
    expect(tops, [...tops]..sort());
  });

  testWidgets('tapping a link opens the other item', (tester) async {
    final opened = <String>[];
    await _pump(
      tester,
      links: [_link('a', WorkItemLinkType.related, number: 3, title: 'Lights')],
      onOpen: opened.add,
    );

    await tester.tap(find.text('#3 Lights'));

    expect(opened, ['item-3']);
  });

  testWidgets('removing a link passes its id', (tester) async {
    final removed = <String>[];
    await _pump(
      tester,
      links: [_link('a', WorkItemLinkType.related, number: 3, title: 'Lights')],
      onRemove: removed.add,
    );

    await tester.tap(find.byTooltip('Remove link'));

    expect(removed, ['a']);
  });

  testWidgets('adding defaults to Related', (tester) async {
    final added = <WorkItemLinkType>[];
    await _pump(tester, onAdd: added.add);

    await tester.tap(find.widgetWithText(TextButton, 'Add link'));

    expect(added, [WorkItemLinkType.related]);
  });

  testWidgets('adding uses the chosen type', (tester) async {
    final added = <WorkItemLinkType>[];
    await _pump(tester, onAdd: added.add);

    await tester.tap(find.byKey(const Key('link-type-picker')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Blocked by').last);
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'Add link'));

    expect(added, [WorkItemLinkType.blockedBy]);
  });
}

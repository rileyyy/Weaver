import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/work_item_picker/work_item_pick.dart';
import 'package:weaver/shared/work_item_picker/work_item_picker_dialog.dart';

import '../fakes/work_items.dart';

final List<WorkItem> _items = [
  workItem('kitchen', number: 5, title: 'Kitchen'),
  workItem('garden', number: 9, title: 'Garden'),
];

/// Opens the picker and reports what it completed with through [onPicked].
Future<List<String>> _openPicker(
  WidgetTester tester, {
  String? noneLabel,
  required void Function(WorkItemPick?) onPicked,
}) async {
  final searched = <String>[];
  Future<List<WorkItem>> search(String text) async {
    searched.add(text);
    return [
      for (final item in _items)
        if (item.title.toLowerCase().contains(text.toLowerCase())) item,
    ];
  }

  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => TextButton(
          onPressed: () async => onPicked(
            await showWorkItemPicker(
              context,
              title: 'Choose a parent',
              search: search,
              noneLabel: noneLabel,
            ),
          ),
          child: const Text('open'),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
  return searched;
}

void main() {
  testWidgets('lists every match before anything is typed', (tester) async {
    final searched = await _openPicker(tester, onPicked: (_) {});

    expect(find.text('Choose a parent'), findsOneWidget);
    expect(find.text('#5 Kitchen'), findsOneWidget);
    expect(find.text('#9 Garden'), findsOneWidget);
    expect(searched, ['']);
  });

  testWidgets('searches once typing pauses', (tester) async {
    final searched = await _openPicker(tester, onPicked: (_) {});

    await tester.enterText(find.byType(TextField), 'g');
    await tester.enterText(find.byType(TextField), 'gar');
    await tester.pumpAndSettle(const Duration(milliseconds: 400));

    expect(searched, ['', 'gar']);
    expect(find.text('#5 Kitchen'), findsNothing);
    expect(find.text('#9 Garden'), findsOneWidget);
  });

  testWidgets('tapping a match picks it', (tester) async {
    WorkItemPick? picked;
    await _openPicker(tester, onPicked: (pick) => picked = pick);

    await tester.tap(find.text('#9 Garden'));
    await tester.pumpAndSettle();

    expect(picked?.item?.id, 'garden');
    expect(find.text('Choose a parent'), findsNothing);
  });

  testWidgets('the none option picks no item', (tester) async {
    WorkItemPick? picked;
    await _openPicker(
      tester,
      noneLabel: 'No parent',
      onPicked: (pick) => picked = pick,
    );

    await tester.tap(find.text('No parent'));
    await tester.pumpAndSettle();

    expect(picked, isNotNull);
    expect(picked!.item, isNull);
  });

  testWidgets('without a none label there is no none option', (tester) async {
    await _openPicker(tester, onPicked: (_) {});

    expect(find.text('No parent'), findsNothing);
  });

  testWidgets('cancelling picks nothing', (tester) async {
    var closed = false;
    WorkItemPick? picked;
    await _openPicker(
      tester,
      noneLabel: 'No parent',
      onPicked: (pick) {
        closed = true;
        picked = pick;
      },
    );

    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();

    expect(closed, isTrue);
    expect(picked, isNull);
  });
}

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/widgets/delete_lookup_dialog.dart';

void main() {
  late List<String?> deletedWith;

  Future<void> open(WidgetTester tester, {String? noneLabel}) async {
    deletedWith = [];
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => TextButton(
            onPressed: () => showDeleteLookupDialog(
              context,
              kind: 'status',
              name: 'Doing',
              noneLabel: noneLabel,
              replacements: const [
                (id: 'todo', name: 'To Do'),
                (id: 'done', name: 'Done'),
              ],
              onDelete: (replacementId) async {
                deletedWith.add(replacementId);
                return null;
              },
            ),
            child: const Text('open'),
          ),
        ),
      ),
    );
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
  }

  testWidgets('without a "none" option, the first replacement is picked', (
    tester,
  ) async {
    await open(tester);

    await tester.tap(find.text('Delete'));
    await tester.pumpAndSettle();

    expect(deletedWith, ['todo']);
  });

  testWidgets('another replacement can be chosen', (tester) async {
    await open(tester);

    await tester.tap(find.text('To Do'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Done').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Delete'));
    await tester.pumpAndSettle();

    expect(deletedWith, ['done']);
  });

  testWidgets('with a "none" option, it is the default', (tester) async {
    await open(tester, noneLabel: 'No priority');

    expect(find.text('No priority'), findsOneWidget);
    await tester.tap(find.text('Delete'));
    await tester.pumpAndSettle();

    expect(deletedWith, [null]);
  });
}

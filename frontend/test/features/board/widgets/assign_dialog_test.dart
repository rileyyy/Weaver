import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/widgets/assign_dialog.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';

void main() {
  testWidgets('marks agent accounts among the people', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => TextButton(
            onPressed: () => showAssignDialog(
              context,
              users: const [
                User(id: 'u1', username: 'alice', kind: UserKind.human),
                User(
                  id: 'a1',
                  username: 'Claude Code (desktop)',
                  kind: UserKind.agent,
                ),
              ],
              currentAssigneeId: null,
              onAssign: (_) async {},
            ),
            child: const Text('Open'),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Open'));
    await tester.pumpAndSettle();

    expect(find.text('Agent'), findsOneWidget);
    final agentRow = find.ancestor(
      of: find.text('Claude Code (desktop)'),
      matching: find.byType(SimpleDialogOption),
    );
    expect(
      find.descendant(of: agentRow, matching: find.text('Agent')),
      findsOneWidget,
    );
  });
}

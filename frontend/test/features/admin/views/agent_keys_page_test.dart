import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/agent_keys_view_model.dart';
import 'package:weaver/features/admin/models/api_key_scope.dart';
import 'package:weaver/features/admin/views/agent_keys_page.dart';

import '../fakes/fake_agent_admin_repository.dart';

void main() {
  late FakeAgentAdminRepository repository;
  late List<String> copied;

  setUp(() {
    repository = FakeAgentAdminRepository(
      [agentAccount('a1', name: 'Claude Code (desktop)')],
      keys: {
        'a1': [
          apiKey(
            'k1',
            label: 'laptop',
            scope: ApiKeyScope.readWrite,
            lastUsedAt: DateTime(2026, 11, 2, 14, 30),
          ),
          apiKey('k2', label: 'old', revokedAt: DateTime(2026, 11, 2)),
        ],
      },
    );
    copied = [];
  });

  Future<void> pumpPage(WidgetTester tester) async {
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
      SystemChannels.platform,
      (call) async {
        if (call.method == 'Clipboard.setData') {
          copied.add((call.arguments as Map)['text'] as String);
        }
        return null;
      },
    );
    addTearDown(
      () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        SystemChannels.platform,
        null,
      ),
    );
    await tester.pumpWidget(
      MaterialApp(
        home: AgentKeysPage(
          agent: repository.agents.single,
          viewModel: AgentKeysViewModel(repository, 'a1'),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('lists each key with its label, scope and dates', (tester) async {
    await pumpPage(tester);

    expect(find.text('Keys for Claude Code (desktop)'), findsOneWidget);
    expect(find.text('laptop'), findsOneWidget);
    expect(find.text('Read-write'), findsOneWidget);
    expect(find.text('Created 2026-11-01'), findsNWidgets(2));
    expect(find.text('Last used 2026-11-02 14:30'), findsOneWidget);
    expect(find.text('Never used'), findsOneWidget);
  });

  testWidgets('a revoked key stays listed, marked revoked, with no revoke', (
    tester,
  ) async {
    await pumpPage(tester);

    expect(find.text('old'), findsOneWidget);
    expect(find.text('Revoked 2026-11-02'), findsOneWidget);
    expect(find.byTooltip('Revoke old'), findsNothing);
  });

  testWidgets('issuing a key shows it once, with copy and a warning', (
    tester,
  ) async {
    await pumpPage(tester);

    await tester.tap(find.text('Issue key'));
    await tester.pumpAndSettle();
    expect(find.text('Reads everything and writes nothing.'), findsOneWidget);

    await tester.enterText(find.widgetWithText(TextField, 'Label'), 'ci');
    await tester.tap(find.text('Read-write').last);
    await tester.pump();
    expect(find.text('Does what a non-admin person can.'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, 'Issue'));
    await tester.pumpAndSettle();

    expect(repository.calls.single, 'issueKey a1 ci ReadWrite');
    expect(find.text('wvr_secret-ci'), findsOneWidget);
    expect(find.textContaining('won’t be shown again'), findsOneWidget);

    await tester.tap(find.byTooltip('Copy key'));
    await tester.pumpAndSettle();
    expect(copied, ['wvr_secret-ci']);
    expect(find.text('Copied'), findsOneWidget);

    await tester.tap(find.text('Done'));
    await tester.pumpAndSettle();

    expect(find.text('wvr_secret-ci'), findsNothing);
    expect(find.text('ci'), findsOneWidget);
  });

  testWidgets('a refused issue keeps the form open with the reason', (
    tester,
  ) async {
    await pumpPage(tester);
    repository.error = const ApiException(
      'Label must be at most 100 characters (400).',
      statusCode: 400,
    );

    await tester.tap(find.text('Issue key'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Label'), 'ci');
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Issue'));
    await tester.pumpAndSettle();

    expect(find.textContaining('at most 100'), findsOneWidget);
    expect(find.widgetWithText(TextField, 'Label'), findsOneWidget);
  });

  testWidgets('revoking asks first, then marks the key revoked', (
    tester,
  ) async {
    await pumpPage(tester);

    await tester.tap(find.byTooltip('Revoke laptop'));
    await tester.pumpAndSettle();
    expect(repository.calls, isEmpty);

    await tester.tap(find.widgetWithText(FilledButton, 'Revoke'));
    await tester.pumpAndSettle();

    expect(repository.calls.single, 'revokeKey a1 k1');
    expect(find.byTooltip('Revoke laptop'), findsNothing);
    expect(find.text('Revoked 2026-11-02'), findsNWidgets(2));
  });

  testWidgets('cancelling a revoke leaves the key alone', (tester) async {
    await pumpPage(tester);

    await tester.tap(find.byTooltip('Revoke laptop'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();

    expect(repository.calls, isEmpty);
    expect(find.byTooltip('Revoke laptop'), findsOneWidget);
  });
}

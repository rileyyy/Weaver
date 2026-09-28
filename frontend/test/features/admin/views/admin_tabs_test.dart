import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/status_admin_view_model.dart';
import 'package:weaver/features/admin/user_admin_view_model.dart';
import 'package:weaver/features/admin/views/statuses_tab.dart';
import 'package:weaver/features/admin/views/users_tab.dart';
import 'package:weaver/shared/data/current_user.dart';

import '../fakes/fake_status_admin_repository.dart';
import '../fakes/fake_user_admin_repository.dart';

class _SignedInAs implements CurrentUser {
  const _SignedInAs(this.currentUserId);

  @override
  final String? currentUserId;
}

Widget _host(Widget child) => MaterialApp(home: Scaffold(body: child));

void main() {
  group('StatusesTab', () {
    testWidgets('lists the statuses with a drag handle each', (tester) async {
      final viewModel = StatusAdminViewModel(
        FakeStatusAdminRepository([
          adminStatus('todo', name: 'To Do'),
          adminStatus('done', name: 'Done'),
        ]),
      );
      addTearDown(viewModel.dispose);
      await viewModel.load();

      await tester.pumpWidget(_host(StatusesTab(viewModel: viewModel)));

      expect(find.text('To Do'), findsOneWidget);
      expect(find.text('Done'), findsOneWidget);
      expect(find.byIcon(Icons.drag_indicator), findsNWidgets(2));
    });

    testWidgets('does not offer deleting the last status', (tester) async {
      final viewModel = StatusAdminViewModel(
        FakeStatusAdminRepository([adminStatus('todo', name: 'To Do')]),
      );
      addTearDown(viewModel.dispose);
      await viewModel.load();

      await tester.pumpWidget(_host(StatusesTab(viewModel: viewModel)));

      final delete = tester.widget<IconButton>(
        find.widgetWithIcon(IconButton, Icons.delete_outline),
      );
      expect(delete.onPressed, isNull);
      expect(find.byTooltip('At least one status must remain'), findsOneWidget);
    });

    testWidgets('delete asks for a replacement among the other statuses', (
      tester,
    ) async {
      final repository = FakeStatusAdminRepository([
        adminStatus('todo', name: 'To Do'),
        adminStatus('doing', name: 'Doing'),
      ]);
      final viewModel = StatusAdminViewModel(repository);
      addTearDown(viewModel.dispose);
      await viewModel.load();
      await tester.pumpWidget(_host(StatusesTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Delete Doing'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete'));
      await tester.pumpAndSettle();

      expect(repository.calls.single, 'delete doing -> todo');
      expect(find.text('Doing'), findsNothing);
    });
  });

  group('UsersTab', () {
    late FakeUserAdminRepository repository;
    late UserAdminViewModel viewModel;

    setUp(() async {
      repository = FakeUserAdminRepository([
        adminUser('admin', isAdmin: true),
        adminUser('carol', lockedUntil: DateTime(2026, 9, 28, 14, 30)),
      ]);
      viewModel = UserAdminViewModel(repository, const _SignedInAs('admin'));
      await viewModel.load();
    });

    tearDown(() => viewModel.dispose());

    testWidgets('marks you, admins and lockouts', (tester) async {
      await tester.pumpWidget(_host(UsersTab(viewModel: viewModel)));

      expect(find.text('You'), findsOneWidget);
      expect(find.text('Admin'), findsOneWidget);
      expect(find.text('Locked until 14:30'), findsOneWidget);
    });

    testWidgets('your own menu has no deactivate or delete', (tester) async {
      await tester.pumpWidget(_host(UsersTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Actions for admin'));
      await tester.pumpAndSettle();

      expect(find.text('Reset password'), findsOneWidget);
      expect(find.text('Deactivate'), findsNothing);
      expect(find.text('Delete'), findsNothing);
    });

    testWidgets('deactivating asks first, then deactivates', (tester) async {
      await tester.pumpWidget(_host(UsersTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Actions for carol'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Deactivate'));
      await tester.pumpAndSettle();
      expect(repository.calls, isEmpty);

      await tester.tap(find.widgetWithText(FilledButton, 'Deactivate'));
      await tester.pumpAndSettle();

      expect(repository.calls.single, 'setActive carol false');
      expect(find.text('Deactivated'), findsOneWidget);
    });

    testWidgets('a refused delete shows the reason', (tester) async {
      await tester.pumpWidget(_host(UsersTab(viewModel: viewModel)));
      repository.error = const ApiException(
        "User 'carol' has written 2 comment(s) (409).",
        statusCode: 409,
      );

      await tester.tap(find.byTooltip('Actions for carol'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Delete'));
      await tester.pumpAndSettle();

      expect(find.textContaining('has written'), findsOneWidget);
      expect(find.text('carol'), findsOneWidget);
    });
  });
}

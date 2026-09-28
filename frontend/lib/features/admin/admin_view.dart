import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/admin/lookup_admin_view_model.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/status_admin_view_model.dart';
import 'package:weaver/features/admin/user_admin_view_model.dart';
import 'package:weaver/features/admin/views/lookup_tab.dart';
import 'package:weaver/features/admin/views/statuses_tab.dart';
import 'package:weaver/features/admin/views/users_tab.dart';

/// Opens the Admin page; completes when it's closed.
Future<void> showAdminPage(BuildContext context) => Navigator.of(
  context,
).push(MaterialPageRoute<void>(builder: (_) => const AdminView()));

/// Statuses, priorities, layers and users, one tab each. Only offered to
/// admins; the server refuses every change from anyone else.
class AdminView extends StatefulWidget {
  const AdminView({super.key});

  @override
  State<AdminView> createState() => _AdminViewState();
}

class _AdminViewState extends State<AdminView> {
  final StatusAdminViewModel _statuses = getIt<StatusAdminViewModel>();
  final LookupAdminViewModel _priorities = getIt<LookupAdminViewModel>(
    param1: LookupKind.priorities,
  );
  final LookupAdminViewModel _layers = getIt<LookupAdminViewModel>(
    param1: LookupKind.layers,
  );
  final UserAdminViewModel _users = getIt<UserAdminViewModel>();

  @override
  void initState() {
    super.initState();
    unawaited(_statuses.load());
    unawaited(_priorities.load());
    unawaited(_layers.load());
    unawaited(_users.load());
  }

  @override
  void dispose() {
    _statuses.dispose();
    _priorities.dispose();
    _layers.dispose();
    _users.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 4,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Admin'),
          bottom: const TabBar(
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            tabs: [
              Tab(text: 'Statuses'),
              Tab(text: 'Priorities'),
              Tab(text: 'Layers'),
              Tab(text: 'Users'),
            ],
          ),
        ),
        body: TabBarView(
          children: [
            StatusesTab(viewModel: _statuses),
            LookupTab(viewModel: _priorities),
            LookupTab(viewModel: _layers),
            UsersTab(viewModel: _users),
          ],
        ),
      ),
    );
  }
}

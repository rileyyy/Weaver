import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/admin/agent_admin_view_model.dart';
import 'package:weaver/features/admin/lookup_admin_view_model.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/server_settings_admin_view_model.dart';
import 'package:weaver/features/admin/space_admin_view_model.dart';
import 'package:weaver/features/admin/status_admin_view_model.dart';
import 'package:weaver/features/admin/user_admin_view_model.dart';
import 'package:weaver/features/admin/views/agents_tab.dart';
import 'package:weaver/features/admin/views/lookup_tab.dart';
import 'package:weaver/features/admin/views/server_tab.dart';
import 'package:weaver/features/admin/views/spaces_tab.dart';
import 'package:weaver/features/admin/views/statuses_tab.dart';
import 'package:weaver/features/admin/views/users_tab.dart';

/// Opens the Admin page; completes when it's closed.
Future<void> showAdminPage(BuildContext context) => Navigator.of(
  context,
).push(MaterialPageRoute<void>(builder: (_) => const AdminView()));

/// Spaces, statuses, priorities, layers, users, agents and server-wide
/// settings, one tab each. Only offered to admins; the server refuses every change from
/// anyone else.
class AdminView extends StatefulWidget {
  const AdminView({super.key});

  @override
  State<AdminView> createState() => _AdminViewState();
}

class _AdminViewState extends State<AdminView> {
  final SpaceAdminViewModel _spaces = getIt<SpaceAdminViewModel>();
  final StatusAdminViewModel _statuses = getIt<StatusAdminViewModel>();
  final LookupAdminViewModel _priorities = getIt<LookupAdminViewModel>(
    param1: LookupKind.priorities,
  );
  final LookupAdminViewModel _layers = getIt<LookupAdminViewModel>(
    param1: LookupKind.layers,
  );
  final UserAdminViewModel _users = getIt<UserAdminViewModel>();
  final AgentAdminViewModel _agents = getIt<AgentAdminViewModel>();
  final ServerSettingsAdminViewModel _server =
      getIt<ServerSettingsAdminViewModel>();

  @override
  void initState() {
    super.initState();
    // Statuses and priorities belong to a Space, so they load once the
    // Spaces have, and follow them when one is added.
    _spaces.addListener(_followSpaces);
    unawaited(_spaces.load());
    unawaited(_layers.load());
    unawaited(_users.load());
    unawaited(_agents.load());
    unawaited(_server.load());
  }

  void _followSpaces() {
    unawaited(_statuses.followSpaces(_spaces.spaces));
    unawaited(_priorities.followSpaces(_spaces.spaces));
  }

  @override
  void dispose() {
    _spaces
      ..removeListener(_followSpaces)
      ..dispose();
    _statuses.dispose();
    _priorities.dispose();
    _layers.dispose();
    _users.dispose();
    _agents.dispose();
    _server.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 7,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Admin'),
          bottom: const TabBar(
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            tabs: [
              Tab(text: 'Spaces'),
              Tab(text: 'Statuses'),
              Tab(text: 'Priorities'),
              Tab(text: 'Layers'),
              Tab(text: 'Users'),
              Tab(text: 'Agents'),
              Tab(text: 'Server'),
            ],
          ),
        ),
        body: TabBarView(
          children: [
            SpacesTab(viewModel: _spaces),
            StatusesTab(viewModel: _statuses, spaces: _spaces),
            LookupTab(viewModel: _priorities, spaces: _spaces),
            LookupTab(viewModel: _layers),
            UsersTab(viewModel: _users),
            AgentsTab(viewModel: _agents),
            ServerTab(viewModel: _server),
          ],
        ),
      ),
    );
  }
}

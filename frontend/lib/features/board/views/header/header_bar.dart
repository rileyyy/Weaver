import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/views/header/widgets/breadcrumb_bar.dart';
import 'package:weaver/features/board/views/header/widgets/search_and_actions.dart';
import 'package:weaver/features/board/views/header/widgets/time_filter_bar.dart';

class HeaderBar extends StatelessWidget {
  const HeaderBar({
    super.key,
    required this.isNarrow,
    required this.breadcrumbs,
    required this.onSelectBreadcrumb,
    required this.searchController,
    required this.onSearchChanged,
    required this.filterStart,
    required this.filterEnd,
    required this.onTimeFilterChanged,
    required this.onTimeFilterCleared,
    required this.onOpenFilters,
    required this.onLogout,
    required this.tabController,
  });

  final bool isNarrow;
  final List<ScopeCrumb> breadcrumbs;
  final Future<void> Function(int index) onSelectBreadcrumb;
  final TextEditingController searchController;
  final ValueChanged<String> onSearchChanged;
  final DateTime? filterStart;
  final DateTime? filterEnd;
  final void Function({DateTime? start, DateTime? end}) onTimeFilterChanged;
  final VoidCallback onTimeFilterCleared;
  final VoidCallback onOpenFilters;
  final VoidCallback onLogout;
  final TabController tabController;

  @override
  Widget build(BuildContext context) {
    final title = Text(
      'Weaver',
      style: Theme.of(
        context,
      ).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
    );

    final navigationStack = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        BreadcrumbBar(breadcrumbs: breadcrumbs, onSelect: onSelectBreadcrumb),
        TimeFilterBar(
          start: filterStart,
          end: filterEnd,
          onChanged: onTimeFilterChanged,
          onClear: onTimeFilterCleared,
        ),
      ],
    );

    final actionsRow = SearchAndActions(
      controller: searchController,
      onSearchChanged: onSearchChanged,
      onOpenFilters: onOpenFilters,
      onLogout: onLogout,
    );

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surfaceContainerHighest,
        border: Border(
          bottom: BorderSide(
            color: Theme.of(context).colorScheme.outlineVariant,
          ),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          isNarrow
              ? Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Center(child: title),
                    const SizedBox(height: 8),
                    navigationStack,
                    const SizedBox(height: 8),
                    actionsRow,
                  ],
                )
              : Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(child: navigationStack),
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: title,
                    ),
                    Expanded(
                      child: Align(
                        alignment: Alignment.topRight,
                        child: actionsRow,
                      ),
                    ),
                  ],
                ),
          const SizedBox(height: 4),
          TabBar(
            controller: tabController,
            isScrollable: isNarrow,
            tabAlignment: isNarrow ? TabAlignment.start : TabAlignment.fill,
            tabs: const [
              Tab(text: 'Swim Lanes'),
              Tab(text: 'Roadmap'),
              Tab(text: 'Hierarchy'),
            ],
          ),
        ],
      ),
    );
  }
}

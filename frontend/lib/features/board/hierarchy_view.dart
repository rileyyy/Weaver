import 'package:flutter/material.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';
import 'package:weaver/features/board/widgets/load_error_view.dart';
import 'package:weaver/features/board/widgets/status_dot.dart';
import 'package:weaver/features/board/widgets/tag_badge.dart';

const double _indentPerLevel = 24;
const double _caretColumnWidth = 24;
const double _caretGap = 8;
const double _resizeHandleWidth = 8;
const double _minColumnWidth = 56;
const double _maxColumnWidth = 640;

const Map<String, double> _defaultColumnWidths = {
  'number': 64,
  'title': 280,
  'status': 140,
  'assignedTo': 160,
  'tags': 200,
};

/// Columns shown after the title column, in order. A future milestone could
/// make this user-configurable; for now it's fixed to Status, Assigned To,
/// then Tags.
enum HierarchyColumn {
  status,
  assignedTo,
  tags;

  String get headerLabel => switch (this) {
        HierarchyColumn.status => 'Status',
        HierarchyColumn.assignedTo => 'Assigned To',
        HierarchyColumn.tags => 'Tags',
      };
}

const List<HierarchyColumn> _trailingColumns = [
  HierarchyColumn.status,
  HierarchyColumn.assignedTo,
  HierarchyColumn.tags,
];

/// Every work item nested under its parent, respecting the same time/search/
/// status filters and sort order as the swim-lane board (see
/// [BoardViewModel.hierarchyRoots]). Loaded lazily by the board view the
/// first time this tab is opened.
///
/// Columns, left to right: a fixed work-item-number column, a title column
/// (whose text indents by depth, with the expand/collapse caret in its own
/// fixed gutter before it), then Status and Assigned To — every column
/// (other than the caret gutter) independently resizable by dragging the
/// header dividers. Column widths are pure view state, like the collapse
/// set below — not persisted.
class HierarchyView extends StatefulWidget {
  const HierarchyView({
    super.key,
    required this.viewModel,
    required this.onItemOpened,
    required this.onAssignRequested,
  });

  final BoardViewModel viewModel;
  final void Function(String workItemId) onItemOpened;
  final void Function(String workItemId, String? currentAssigneeId) onAssignRequested;

  @override
  State<HierarchyView> createState() => _HierarchyViewState();
}

class _HierarchyViewState extends State<HierarchyView> {
  final Set<String> _collapsedIds = {};
  final Map<String, double> _columnWidths = {..._defaultColumnWidths};

  void _resizeColumn(String key, double deltaX) {
    setState(() {
      final current = _columnWidths[key] ?? _minColumnWidth;
      _columnWidths[key] = (current + deltaX).clamp(_minColumnWidth, _maxColumnWidth);
    });
  }

  @override
  Widget build(BuildContext context) {
    final viewModel = widget.viewModel;

    if (viewModel.isHierarchyLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    final error = viewModel.hierarchyLoadError;
    if (error != null) {
      return LoadErrorView(message: error, onRetry: viewModel.loadHierarchy);
    }

    final rows = <_HierarchyRow>[];
    void flatten(List<HierarchyNode> nodes, int depth) {
      for (final node in nodes) {
        rows.add(_HierarchyRow(node: node, depth: depth));
        if (node.children.isNotEmpty && !_collapsedIds.contains(node.item.id)) {
          flatten(node.children, depth + 1);
        }
      }
    }

    flatten(viewModel.hierarchyRoots, 0);

    return Column(
      children: [
        _HierarchyHeaderRow(columnWidths: _columnWidths, onResize: _resizeColumn),
        Expanded(
          child: rows.isEmpty
              ? const Center(child: Text('No work items match the current filters.'))
              : ListView.builder(
                  itemCount: rows.length,
                  itemBuilder: (context, index) {
                    final row = rows[index];
                    final node = row.node;
                    return _HierarchyItemTile(
                      node: node,
                      depth: row.depth,
                      columnWidths: _columnWidths,
                      isCollapsed: _collapsedIds.contains(node.item.id),
                      onToggleCollapsed: () => setState(() {
                        if (!_collapsedIds.remove(node.item.id)) _collapsedIds.add(node.item.id);
                      }),
                      onTap: () => widget.onItemOpened(node.item.id),
                      onAssignTapped: () =>
                          widget.onAssignRequested(node.item.id, node.item.assignedToUserId),
                      viewModel: viewModel,
                    );
                  },
                ),
        ),
      ],
    );
  }
}

/// One flattened row: a [HierarchyNode] paired with how deep it sits in the
/// (currently expanded) tree, for [ListView.builder] to render linearly.
class _HierarchyRow {
  const _HierarchyRow({required this.node, required this.depth});

  final HierarchyNode node;
  final int depth;
}

class _HierarchyHeaderRow extends StatelessWidget {
  const _HierarchyHeaderRow({required this.columnWidths, required this.onResize});

  final Map<String, double> columnWidths;
  final void Function(String key, double deltaX) onResize;

  @override
  Widget build(BuildContext context) {
    final style = Theme.of(context).textTheme.labelLarge?.copyWith(fontWeight: FontWeight.w600);
    return Container(
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: Theme.of(context).colorScheme.outlineVariant)),
      ),
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 12),
      child: Row(
        children: [
          const SizedBox(width: _caretColumnWidth + _caretGap),
          _HeaderCell(label: '#', width: columnWidths['number']!, style: style),
          _ResizeHandle(onDrag: (dx) => onResize('number', dx)),
          _HeaderCell(label: 'Title', width: columnWidths['title']!, style: style),
          _ResizeHandle(onDrag: (dx) => onResize('title', dx)),
          for (final column in _trailingColumns) ...[
            _HeaderCell(label: column.headerLabel, width: columnWidths[column.name]!, style: style),
            _ResizeHandle(onDrag: (dx) => onResize(column.name, dx)),
          ],
          const Expanded(child: SizedBox()),
        ],
      ),
    );
  }
}

class _HeaderCell extends StatelessWidget {
  const _HeaderCell({required this.label, required this.width, required this.style});

  final String label;
  final double width;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      child: Text(label, style: style, overflow: TextOverflow.ellipsis),
    );
  }
}

/// A narrow draggable divider between two header cells. Only the header
/// carries resize handles — body rows read the same [columnWidths] map, so
/// dragging a header divider resizes every row's cell in that column at
/// once.
class _ResizeHandle extends StatelessWidget {
  const _ResizeHandle({required this.onDrag});

  final ValueChanged<double> onDrag;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.resizeColumn,
      child: GestureDetector(
        behavior: HitTestBehavior.translucent,
        onHorizontalDragUpdate: (details) => onDrag(details.delta.dx),
        child: SizedBox(
          width: _resizeHandleWidth,
          child: Center(
            child: Container(width: 1, color: Theme.of(context).colorScheme.outlineVariant),
          ),
        ),
      ),
    );
  }
}

class _HierarchyItemTile extends StatelessWidget {
  const _HierarchyItemTile({
    required this.node,
    required this.depth,
    required this.columnWidths,
    required this.isCollapsed,
    required this.onToggleCollapsed,
    required this.onTap,
    required this.onAssignTapped,
    required this.viewModel,
  });

  final HierarchyNode node;
  final int depth;
  final Map<String, double> columnWidths;
  final bool isCollapsed;
  final VoidCallback onToggleCollapsed;
  final VoidCallback onTap;
  final VoidCallback onAssignTapped;
  final BoardViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    final hasChildren = node.children.isNotEmpty;
    final item = node.item;

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 6, horizontal: 12),
        child: Row(
          children: [
            // Fixed height/width regardless of whether this row has a
            // caret — IconButton's default 48x48 minimum tap target would
            // otherwise make rows with children taller than leaf rows. Its
            // position never shifts with depth — see the class doc comment.
            SizedBox(
              width: _caretColumnWidth,
              height: _caretColumnWidth,
              child: hasChildren
                  ? IconButton(
                      padding: EdgeInsets.zero,
                      iconSize: 18,
                      constraints: const BoxConstraints(),
                      visualDensity: VisualDensity.compact,
                      icon: Icon(isCollapsed ? Icons.chevron_right : Icons.expand_more),
                      tooltip: isCollapsed ? 'Expand' : 'Collapse',
                      onPressed: onToggleCollapsed,
                    )
                  : null,
            ),
            const SizedBox(width: _caretGap),
            SizedBox(
              width: columnWidths['number'],
              child: Text('#${item.number}', overflow: TextOverflow.ellipsis),
            ),
            const SizedBox(width: _resizeHandleWidth),
            // Depth only indents the title's own text — not the caret or
            // number column before it, nor the columns after it — so every
            // column stays aligned under its header regardless of nesting.
            SizedBox(
              width: columnWidths['title'],
              child: Padding(
                padding: EdgeInsets.only(left: depth * _indentPerLevel),
                child: Text(item.title, overflow: TextOverflow.ellipsis),
              ),
            ),
            const SizedBox(width: _resizeHandleWidth),
            for (final column in _trailingColumns) ...[
              SizedBox(
                width: columnWidths[column.name],
                child: _ColumnValue(
                  column: column,
                  item: item,
                  viewModel: viewModel,
                  onAssignTapped: onAssignTapped,
                ),
              ),
              const SizedBox(width: _resizeHandleWidth),
            ],
            const Expanded(child: SizedBox()),
          ],
        ),
      ),
    );
  }
}

class _ColumnValue extends StatelessWidget {
  const _ColumnValue({
    required this.column,
    required this.item,
    required this.viewModel,
    required this.onAssignTapped,
  });

  final HierarchyColumn column;
  final HierarchyItem item;
  final BoardViewModel viewModel;
  final VoidCallback onAssignTapped;

  @override
  Widget build(BuildContext context) {
    return switch (column) {
      HierarchyColumn.status => Row(
          children: [
            StatusDot(color: viewModel.statusColorFor(item.statusId)),
            const SizedBox(width: 6),
            Flexible(
              child: Text(
                viewModel.statusNameFor(item.statusId) ?? '',
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          ],
        ),
      HierarchyColumn.assignedTo => Row(
          children: [
            AssigneeAvatar(
              initial: viewModel.assigneeInitialFor(item.assignedToUserId),
              showPlaceholderWhenUnassigned: true,
              onTap: onAssignTapped,
            ),
            const SizedBox(width: 6),
            Flexible(
              child: Text(
                viewModel.usernameFor(item.assignedToUserId) ?? 'Unassigned',
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          ],
        ),
      HierarchyColumn.tags => TagBadgeRow(tags: item.tags),
    };
  }
}

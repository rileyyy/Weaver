import 'dart:math' as math;

import 'package:flutter/material.dart';

/// The search field takes this share of the width left beside the buttons.
const double searchWidthFraction = 1 / 3;

/// A floor so the field stays usable where a third of the row is too small
/// to type in, e.g. the stacked phone layout.
const double minSearchWidth = 160;

class SearchAndActions extends StatelessWidget {
  const SearchAndActions({
    super.key,
    required this.controller,
    required this.onSearchChanged,
    required this.onOpenFilters,
    required this.onLogout,
  });

  final TextEditingController controller;
  final ValueChanged<String> onSearchChanged;
  final VoidCallback onOpenFilters;
  final VoidCallback onLogout;

  @override
  Widget build(BuildContext context) {
    final searchField = TextField(
      controller: controller,
      onChanged: onSearchChanged,
      decoration: const InputDecoration(
        isDense: true,
        prefixIcon: Icon(Icons.search),
        hintText: 'Search',
        border: OutlineInputBorder(),
      ),
    );

    return Row(
      children: [
        // The field's width is derived from the space the header gives this
        // row rather than fixed: a fixed width overflowed at desktop widths
        // where the header is still in its three-section arrangement.
        Expanded(
          child: LayoutBuilder(
            builder: (context, constraints) => Align(
              alignment: Alignment.centerRight,
              child: SizedBox(
                width: searchWidthFor(constraints.maxWidth),
                child: searchField,
              ),
            ),
          ),
        ),
        const SizedBox(width: 8),
        IconButton(
          icon: const Icon(Icons.filter_list),
          tooltip: 'Filters',
          onPressed: onOpenFilters,
        ),
        IconButton(
          icon: const Icon(Icons.logout),
          tooltip: 'Sign out',
          onPressed: onLogout,
        ),
      ],
    );
  }
}

double searchWidthFor(double availableWidth) => math.max(
  availableWidth * searchWidthFraction,
  math.min(minSearchWidth, availableWidth),
);

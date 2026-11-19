import { useCallback, useMemo, useState } from "react";
import { getTestTypes, updateTestType } from "../../services/testTypeService";
import type { TestType } from "../../types/TestType";
import { useAsyncData, useTableFilters } from "../../hooks";
import PageHero from "../common/PageHero";
import TableToolbar from "../common/TableToolbar";
import ErrorAlert from "../common/ErrorAlert";
import EmptyState from "../common/EmptyState";
import DataTableContainer from "../common/DataTableContainer";
import DataTableRowContextMenu, {
  type DataTableRowAction,
} from "../common/DataTableRowContextMenu";
import {
  copyToClipboard,
  useRowContextMenu,
} from "../common/useRowContextMenu";
import TestTypeRow from "./TestTypeRow";
import {
  ClipboardList as TestTypeIcon,
  Copy,
  Pencil,
} from "lucide-react";
import { canManageReferenceData } from "../../utils/permissions";
import { useAuth } from "../../context/useAuth";
import { Card } from "../ui/card";

type Props = {
  accessToken: string;
  userRole?: string | null;
};

type EditForm = {
  testTypeTitle: string;
  testTypeDescription: string;
  testTypeFees: string;
};

type SearchBy = "all" | "title" | "id" | "desc";

const SEARCH_OPTIONS: { value: SearchBy; label: string }[] = [
  { value: "all", label: "All Fields" },
  { value: "title", label: "Test Title" },
  { value: "id", label: "Test ID" },
  { value: "desc", label: "Description" },
];

export default function TestTypesPage({ accessToken, userRole }: Props) {
  const { user } = useAuth();
  const effectiveRole = userRole ?? user?.role;
  const canEdit = canManageReferenceData("Test Types", effectiveRole);

  const [success, setSuccess] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<EditForm>({
    testTypeTitle: "",
    testTypeDescription: "",
    testTypeFees: "",
  });

  const fetcher = useCallback(() => getTestTypes(accessToken), [accessToken]);

  const {
    data: items,
    loading,
    error,
    refresh,
    setError,
  } = useAsyncData(fetcher, {
    enabled: Boolean(accessToken),
    initialData: [],
    defaultErrorMessage: "Failed to load test types.",
  });

  const {
    search,
    setSearch,
    searchField: searchBy,
    setSearchField: setSearchBy,
    filteredData: filteredItems,
    isFiltered,
    resetFilters: handleResetFilters,
  } = useTableFilters<TestType, SearchBy>({
    data: items,
    initialSearchField: "all",
    filterItem: (item, query, field) => {
      if (!query) return true;

      if (field === "title") {
        return item.testTypeTitle.toLowerCase().includes(query);
      }
      if (field === "id") {
        return String(item.testTypeId).includes(query);
      }
      if (field === "desc") {
        return item.testTypeDescription.toLowerCase().includes(query);
      }

      // 'all'
      return (
        item.testTypeTitle.toLowerCase().includes(query) ||
        item.testTypeDescription.toLowerCase().includes(query) ||
        String(item.testTypeId).includes(query)
      );
    },
  });

  const placeholderText = useMemo(() => {
    switch (searchBy) {
      case "title":
        return "Search by test type title...";
      case "id":
        return "Search by test type ID...";
      case "desc":
        return "Search by description...";
      default:
        return "Search by title, description, or ID...";
    }
  }, [searchBy]);

  function startEditing(item: TestType) {
    if (!canEdit) return;
    setEditingId(item.testTypeId);
    setForm({
      testTypeTitle: item.testTypeTitle,
      testTypeDescription: item.testTypeDescription,
      testTypeFees: String(item.testTypeFees),
    });
    setError("");
    setSuccess("");
  }

  async function saveChanges() {
    if (!canEdit || editingId === null || saving) return;

    const title = form.testTypeTitle.trim();
    const description = form.testTypeDescription.trim();
    const fees = Number(form.testTypeFees);

    if (!title) {
      setError("Test type title is required.");
      return;
    }

    if (form.testTypeFees.trim() === "" || !Number.isFinite(fees) || fees < 0) {
      setError("Enter a valid fee greater than or equal to zero.");
      return;
    }

    setSaving(true);
    setError("");
    setSuccess("");

    try {
      await updateTestType(accessToken, editingId, {
        testTypeTitle: title,
        testTypeDescription: description,
        testTypeFees: fees,
      });

      setEditingId(null);
      setSuccess("Test type updated successfully.");
      void refresh();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to update test type.",
      );
    } finally {
      setSaving(false);
    }
  }

  const rowContextMenu = (): DataTableRowAction<TestType>[] => {
    const actions: DataTableRowAction<TestType>[] = [];

    if (canEdit) {
      actions.push({
        key: "edit",
        label: "Edit Test Type",
        icon: <Pencil className="size-4" />,
        separatorAfter: true,
        onClick: (row) => startEditing(row),
      });
    }

    actions.push(
      {
        key: "copy-title",
        label: "Copy Test Title",
        icon: <Copy className="size-4" />,
        onClick: (row) => {
          void copyToClipboard(row.testTypeTitle).then((copied) => {
            if (copied) {
              setError("");
              setSuccess(`Copied "${row.testTypeTitle}" to clipboard.`);
            }
          });
        },
      },
      {
        key: "copy-fees",
        label: "Copy Test Fee",
        icon: <Copy className="size-4" />,
        onClick: (row) => {
          void copyToClipboard(String(row.testTypeFees)).then((copied) => {
            if (copied) {
              setError("");
              setSuccess(`Copied fee "${row.testTypeFees}" to clipboard.`);
            }
          });
        },
      },
      {
        key: "copy-id",
        label: "Copy Test ID",
        icon: <Copy className="size-4" />,
        onClick: (row) => {
          void copyToClipboard(String(row.testTypeId)).then((copied) => {
            if (copied) {
              setError("");
              setSuccess(`Copied ID #${row.testTypeId} to clipboard.`);
            }
          });
        },
      },
    );

    return actions;
  };

  const {
    contextMenuState,
    handleRowContextMenu,
    handleRowContextMenuKeyDown,
    handleOpenChange,
  } = useRowContextMenu<TestType>(rowContextMenu);

  return (
    <section className="page-content">
      <PageHero pageId="Test Types" />

      <Card className="panel border-[#1b2f52] bg-[#0c192e]">
        <div className="panel-header">
          <div>
            <h2>Test Types Directory</h2>
            <p>Tests configured in DVLD</p>
          </div>

          <span className="record-count">
            {loading
              ? "Loading..."
              : filteredItems.length === items.length
                ? `${items.length} Test Types`
                : `${filteredItems.length} of ${items.length} Test Types`}
          </span>
        </div>

        <TableToolbar<SearchBy>
          searchValue={search}
          onSearchChange={setSearch}
          searchPlaceholder={placeholderText}
          searchField={searchBy}
          searchFieldOptions={SEARCH_OPTIONS}
          onSearchFieldChange={setSearchBy}
          onReset={handleResetFilters}
          isFiltered={isFiltered}
          onRefresh={() => {
            setSuccess("");
            void refresh();
          }}
          refreshing={loading}
        />

        {error && (
          <div style={{ margin: "0 25px 20px" }}>
            <ErrorAlert
              message={error}
              onRetry={() => {
                setSuccess("");
                void refresh();
              }}
            />
          </div>
        )}

        {success && (
          <p
            role="status"
            style={{
              padding: "0 25px 20px",
              margin: 0,
              color: "#059669",
              fontWeight: 600,
              fontSize: 13,
            }}
          >
            {success}
          </p>
        )}

        {loading ? (
          <p className="empty-state">Loading test types...</p>
        ) : (
          <DataTableContainer minWidth={820}>
            <table className="people-table">
              <thead>
                <tr>
                  <th className="w-16 col-compact">ID</th>
                  <th className="w-48 whitespace-nowrap">Test Type</th>
                  <th className="table-cell-description">Description</th>
                  <th className="w-28 whitespace-nowrap">Fees</th>
                  {canEdit && (
                    <th className="w-32 whitespace-nowrap">Actions</th>
                  )}
                </tr>
              </thead>

              <tbody>
                {filteredItems.map((item) => (
                  <TestTypeRow
                    key={item.testTypeId}
                    item={item}
                    isEditing={canEdit && editingId === item.testTypeId}
                    canEdit={canEdit}
                    editForm={form}
                    saving={saving}
                    onStartEdit={startEditing}
                    onFormChange={setForm}
                    onSave={() => void saveChanges()}
                    onCancel={() => {
                      setEditingId(null);
                      setError("");
                    }}
                    hasContextMenu
                    isContextMenuOpen={
                      contextMenuState.open &&
                      contextMenuState.row?.testTypeId === item.testTypeId
                    }
                    onContextMenu={handleRowContextMenu}
                    onContextMenuKeyDown={handleRowContextMenuKeyDown}
                  />
                ))}

                {filteredItems.length === 0 && (
                  <tr>
                    <td colSpan={canEdit ? 5 : 4}>
                      <EmptyState
                        icon={<TestTypeIcon size={36} />}
                        message={
                          items.length === 0
                            ? "No test types found."
                            : "No test types match your search."
                        }
                      />
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </DataTableContainer>
        )}

        {!loading && !error && (
          <div className="panel-footer">
            Showing {filteredItems.length} of {items.length} test types
          </div>
        )}
      </Card>

      <DataTableRowContextMenu<TestType>
        open={contextMenuState.open}
        menuKey={contextMenuState.openKey}
        onOpenChange={handleOpenChange}
        position={{ x: contextMenuState.x, y: contextMenuState.y }}
        row={contextMenuState.row}
        actions={contextMenuState.actions}
        restoreFocusElement={contextMenuState.restoreElement}
      />
    </section>
  );
}

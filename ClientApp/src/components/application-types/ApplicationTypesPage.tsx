import { useCallback, useMemo, useState } from "react";
import {
  getApplicationTypes,
  updateApplicationType,
} from "../../services/applicationTypeService";
import type { ApplicationType } from "../../types/ApplicationType";
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
import ApplicationTypeRow from "./ApplicationTypeRow";
import {
  Copy,
  FileText as ApplicationsIcon,
  Pencil,
} from "lucide-react";
import { canManageReferenceData } from "../../utils/permissions";
import { useAuth } from "../../context/useAuth";
import { Card } from "../ui/card";

type ApplicationTypesPageProps = {
  accessToken: string;
  userRole?: string | null;
};

type EditForm = {
  applicationTypeTitle: string;
  applicationTypeFees: string;
};

type SearchBy = "all" | "title" | "id";

const SEARCH_OPTIONS: { value: SearchBy; label: string }[] = [
  { value: "all", label: "All Fields" },
  { value: "title", label: "Type Name" },
  { value: "id", label: "Type ID" },
];

export default function ApplicationTypesPage({
  accessToken,
  userRole,
}: ApplicationTypesPageProps) {
  const { user } = useAuth();
  const effectiveRole = userRole ?? user?.role;
  const canEdit = canManageReferenceData("Application Types", effectiveRole);

  const [success, setSuccess] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editForm, setEditForm] = useState<EditForm>({
    applicationTypeTitle: "",
    applicationTypeFees: "",
  });
  const [saving, setSaving] = useState(false);

  const fetcher = useCallback(
    () => getApplicationTypes(accessToken),
    [accessToken],
  );

  const {
    data: items,
    loading,
    error,
    refresh,
    setError,
  } = useAsyncData(fetcher, {
    enabled: Boolean(accessToken),
    initialData: [],
    defaultErrorMessage: "Failed to load application types.",
  });

  const {
    search,
    setSearch,
    searchField: searchBy,
    setSearchField: setSearchBy,
    filteredData: filteredItems,
    isFiltered,
    resetFilters: handleResetFilters,
  } = useTableFilters<ApplicationType, SearchBy>({
    data: items,
    initialSearchField: "all",
    filterItem: (item, query, field) => {
      if (!query) return true;

      if (field === "title") {
        return item.applicationTypeTitle.toLowerCase().includes(query);
      }
      if (field === "id") {
        return String(item.applicationTypeId).includes(query);
      }

      // 'all'
      return (
        item.applicationTypeTitle.toLowerCase().includes(query) ||
        String(item.applicationTypeId).includes(query)
      );
    },
  });

  const placeholderText = useMemo(() => {
    switch (searchBy) {
      case "title":
        return "Search by application type name...";
      case "id":
        return "Search by application type ID...";
      default:
        return "Search by type name or ID...";
    }
  }, [searchBy]);

  function startEditing(item: ApplicationType) {
    if (!canEdit) return;
    setEditingId(item.applicationTypeId);
    setEditForm({
      applicationTypeTitle: item.applicationTypeTitle,
      applicationTypeFees: String(item.applicationTypeFees),
    });
    setError("");
    setSuccess("");
  }

  async function handleSave() {
    if (!canEdit || editingId === null || saving) return;

    const title = editForm.applicationTypeTitle.trim();
    const fees = Number(editForm.applicationTypeFees);

    if (!title) {
      setError("Application type name is required.");
      return;
    }

    if (
      editForm.applicationTypeFees.trim() === "" ||
      !Number.isFinite(fees) ||
      fees < 0
    ) {
      setError("Enter a valid fee greater than or equal to zero.");
      return;
    }

    setSaving(true);
    setError("");
    setSuccess("");

    try {
      await updateApplicationType(accessToken, editingId, {
        applicationTypeTitle: title,
        applicationTypeFees: fees,
      });

      await refresh();
      setEditingId(null);
      setSuccess("Application type updated successfully.");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Failed to update application type.",
      );
    } finally {
      setSaving(false);
    }
  }

  const rowContextMenu = (): DataTableRowAction<ApplicationType>[] => {
    const actions: DataTableRowAction<ApplicationType>[] = [];

    if (canEdit) {
      actions.push({
        key: "edit",
        label: "Edit Application Type",
        icon: <Pencil className="size-4" />,
        separatorAfter: true,
        onClick: (row) => startEditing(row),
      });
    }

    actions.push(
      {
        key: "copy-title",
        label: "Copy Type Name",
        icon: <Copy className="size-4" />,
        onClick: (row) => {
          void copyToClipboard(row.applicationTypeTitle).then((copied) => {
            if (copied) {
              setError("");
              setSuccess(
                `Copied "${row.applicationTypeTitle}" to clipboard.`,
              );
            }
          });
        },
      },
      {
        key: "copy-fees",
        label: "Copy Application Fee",
        icon: <Copy className="size-4" />,
        onClick: (row) => {
          void copyToClipboard(String(row.applicationTypeFees)).then((copied) => {
            if (copied) {
              setError("");
              setSuccess(`Copied fee "${row.applicationTypeFees}" to clipboard.`);
            }
          });
        },
      },
      {
        key: "copy-id",
        label: "Copy Type ID",
        icon: <Copy className="size-4" />,
        onClick: (row) => {
          void copyToClipboard(String(row.applicationTypeId)).then((copied) => {
            if (copied) {
              setError("");
              setSuccess(`Copied ID #${row.applicationTypeId} to clipboard.`);
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
  } = useRowContextMenu<ApplicationType>(rowContextMenu);

  return (
    <section className="page-content">
      <PageHero pageId="Application Types" />

      <Card className="panel border-[#1b2f52] bg-[#0c192e]">
        <div className="panel-header">
          <div>
            <h2>Application Types Directory</h2>
            <p>Application types registered in DVLD</p>
          </div>

          <span className="record-count">
            {loading
              ? "Loading..."
              : filteredItems.length === items.length
                ? `${items.length} Types`
                : `${filteredItems.length} of ${items.length} Types`}
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
          <p className="empty-state">Loading application types...</p>
        ) : (
          <DataTableContainer minWidth={680}>
            <table className="people-table">
              <thead>
                <tr>
                  <th className="w-16 col-compact">ID</th>
                  <th>Application Type</th>
                  <th className="w-28 whitespace-nowrap">Fees</th>
                  {canEdit && (
                    <th className="w-32 whitespace-nowrap">Actions</th>
                  )}
                </tr>
              </thead>

              <tbody>
                {filteredItems.map((item) => (
                  <ApplicationTypeRow
                    key={item.applicationTypeId}
                    item={item}
                    isEditing={canEdit && editingId === item.applicationTypeId}
                    canEdit={canEdit}
                    editForm={editForm}
                    saving={saving}
                    onStartEdit={startEditing}
                    onFormChange={setEditForm}
                    onSave={() => void handleSave()}
                    onCancel={() => {
                      setEditingId(null);
                      setError("");
                    }}
                    hasContextMenu
                    isContextMenuOpen={
                      contextMenuState.open &&
                      contextMenuState.row?.applicationTypeId ===
                        item.applicationTypeId
                    }
                    onContextMenu={handleRowContextMenu}
                    onContextMenuKeyDown={handleRowContextMenuKeyDown}
                  />
                ))}

                {filteredItems.length === 0 && (
                  <tr>
                    <td colSpan={canEdit ? 4 : 3}>
                      <EmptyState
                        icon={<ApplicationsIcon size={36} />}
                        message={
                          items.length === 0
                            ? "No application types found."
                            : "No application types match your search."
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
            Showing {filteredItems.length} of {items.length} application types
          </div>
        )}
      </Card>

      <DataTableRowContextMenu<ApplicationType>
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

#!/usr/bin/env bash
set -e

# Persistence verification script for Git Bash
# - Default port: 5023 (prompts for custom port)
# - Prompts for manager email and password
# - Prefers jq for JSON parsing; offers instructions if jq missing
# - Uses unique client-side markers (names with timestamp) to verify persistence
# - Optional cleanup: attempts to delete created order(s) and inventory item(s)

DEFAULT_PORT="5023"
DEFAULT_MANAGER_EMAIL="admin@example.com"

read -rp "API port [${DEFAULT_PORT}]: " API_PORT
API_PORT=${API_PORT:-$DEFAULT_PORT}

read -rp "Manager email [${DEFAULT_MANAGER_EMAIL}]: " MANAGER_USERNAME
MANAGER_USERNAME=${MANAGER_USERNAME:-$DEFAULT_MANAGER_EMAIL}

# Prompt for manager password (do not echo)
read -rsp "Manager password (input hidden): " MANAGER_PASSWORD
echo
if [ -z "$MANAGER_PASSWORD" ]; then
  echo "No manager password provided; exiting."
  exit 1
fi

BASE_URL="http://localhost:${API_PORT}"
echo
echo "Using API base URL: $BASE_URL"
echo "Manager: $MANAGER_USERNAME"
echo

# Detect jq
if command -v jq >/dev/null 2>&1; then
  HAS_JQ=true
  echo "jq found: JSON parsing will use jq."
else
  HAS_JQ=false
  echo
  echo "=================================================================="
  echo "NOTICE: jq is not installed. jq makes JSON parsing in this script"
  echo "reliable and robust. It's strongly recommended to install jq before"
  echo "running this script for best results."
  echo
  echo "Quick install hints for Windows:"
  echo " - Chocolatey (Admin PowerShell): choco install jq -y"
  echo " - Scoop (PowerShell): scoop install jq"
  echo " - Manual: download jq.exe from https://stedolan.github.io/jq/ and place"
  echo "   it in a folder on your PATH (for example C:\\tools\\bin)."
  echo
  echo "If you choose to continue without jq the script will use a fallback"
  echo "parser which works for most typical responses but is less reliable."
  echo "=================================================================="
  echo

  # Prompt user whether to continue without jq
  while true; do
    read -rp "Continue without jq? (y/N): " yn
    case $yn in
      [Yy]* ) echo "Continuing without jq (using fallback parser)"; break;;
      [Nn]* | "" ) echo "Please install jq and re-run the script. Exiting."; exit 1;;
      * ) echo "Please answer y or n.";;
    esac
  done
fi
echo

# Helper to extract JSON field using jq or crude parsing / python fallback
json_get() {
  local key="$1"; local json="$2"
  if [ "$HAS_JQ" = true ]; then
    echo "$json" | jq -r "$key"
    return
  fi

  # Try Python fallback if available (handles nested/quoted JSON reliably)
  if command -v python3 >/dev/null 2>&1; then
    local cleanKey
    cleanKey=$(echo "$key" | sed 's/^\.\?//')
    python3 - <<PYTHON
import sys, json
try:
    obj=json.loads(sys.stdin.read())
    val=obj.get("$cleanKey")
    if val is None:
        print("")
    else:
        print(val)
except Exception:
    print("")
PYTHON
    return
  fi

  # Last-resort crude sed extraction
  local cleanKey
  cleanKey=$(echo "$key" | sed 's/^\.\?//')
  echo "$json" | sed -n "s/.*\"${cleanKey}\"[[:space:]]*:[[:space:]]*\"\?\([^\",}]*\)\"?.*/\1/p" | head -n1
}

# Create unique marker for this run (timestamp + random)
UNIQUE_SUFFIX="$(date -u +"%Y%m%dT%H%M%SZ")-$(printf '%04x' $((RANDOM*RANDOM/1000)) )"
ITEM_BASE_NAME="PersistTest Item ${UNIQUE_SUFFIX}"
ORDER_CUSTOMER="PersistTest Customer ${UNIQUE_SUFFIX}"
ORDER_ITEM_NAME="PersistTest Item from Order ${UNIQUE_SUFFIX}"

# Track created IDs (may be empty if API didn't return them)
CREATED_ITEM_IDS=()
CREATED_ORDER_IDS=()

echo "Unique test suffix: $UNIQUE_SUFFIX"
echo

echo "1) Logging in to obtain JWT..."
LOGIN_PAYLOAD="{ \"userName\": \"${MANAGER_USERNAME}\", \"password\": \"${MANAGER_PASSWORD}\" }"

login_res=$(curl -s -w "\n%{http_code}" -X POST -H "Content-Type: application/json" -d "$LOGIN_PAYLOAD" "$BASE_URL/api/auth/login")
http_status=$(echo "$login_res" | tail -n1)
body=$(echo "$login_res" | sed '$d')

if [ "$http_status" != "200" ]; then
  echo "Login failed (HTTP $http_status). Response:"
  echo "$body"
  exit 1
fi

TOKEN=$(json_get '.token' "$body")
if [ -z "$TOKEN" ] || [ "$TOKEN" = "null" ]; then
  echo "Failed to extract token from login response. Response body:"
  echo "$body"
  exit 1
fi
echo "JWT obtained."
AUTH_HEADER="Authorization: Bearer $TOKEN"

echo
echo "2) Creating inventory item (with unique name)..."
if [ "$HAS_JQ" = true ]; then
  INV_PAYLOAD=$(jq -n --arg name "$ITEM_BASE_NAME" --arg location "TestShelf-A1" --argjson qty 42 '{ name: $name, quantity: $qty, location: $location }')
else
  INV_PAYLOAD="{ \"name\": \"${ITEM_BASE_NAME}\", \"quantity\": 42, \"location\": \"TestShelf-A1\" }"
fi

inv_res=$(curl -s -w "\n%{http_code}" -X POST -H "Content-Type: application/json" -H "$AUTH_HEADER" -d "$INV_PAYLOAD" "$BASE_URL/api/inventory")
inv_status=$(echo "$inv_res" | tail -n1)
inv_body=$(echo "$inv_res" | sed '$d')

if [ "$inv_status" != "201" ]; then
  echo "Inventory creation failed (HTTP $inv_status). Response:"
  echo "$inv_body"
  exit 1
fi

ITEM_ID=$(json_get '.itemId' "$inv_body")
if [ -n "$ITEM_ID" ] && [ "$ITEM_ID" != "null" ]; then
  CREATED_ITEM_IDS+=("$ITEM_ID")
  echo "Created Inventory ItemId = $ITEM_ID"
else
  echo "Inventory created but no numeric ItemId returned; will verify by unique name."
fi

echo
echo "3) Creating an order that contains a new item (unique names)..."
if [ "$HAS_JQ" = true ]; then
  ORDER_PAYLOAD=$(jq -n --arg customer "$ORDER_CUSTOMER" --arg itemName "$ORDER_ITEM_NAME" --arg location "OrderShelf-B2" --argjson qty 5 '{ customerName: $customer, items: [ { name: $itemName, quantity: $qty, location: $location } ] }')
else
  ORDER_PAYLOAD="{ \"customerName\": \"${ORDER_CUSTOMER}\", \"items\": [ { \"name\": \"${ORDER_ITEM_NAME}\", \"quantity\": 5, \"location\": \"OrderShelf-B2\" } ] }"
fi

order_res=$(curl -s -w "\n%{http_code}" -X POST -H "Content-Type: application/json" -H "$AUTH_HEADER" -d "$ORDER_PAYLOAD" "$BASE_URL/api/order")
order_status=$(echo "$order_res" | tail -n1)
order_body=$(echo "$order_res" | sed '$d')

if [ "$order_status" != "201" ]; then
  echo "Order creation failed (HTTP $order_status). Response:"
  echo "$order_body"
  exit 1
fi

ORDER_ID=$(json_get '.orderId' "$order_body")
if [ -n "$ORDER_ID" ] && [ "$ORDER_ID" != "null" ]; then
  CREATED_ORDER_IDS+=("$ORDER_ID")
  echo "Created OrderId = $ORDER_ID"
else
  echo "Order created but no numeric OrderId returned; will verify by unique customer/item name."
fi

echo
echo "4) Now: stop your app server (CTRL+C in the terminal running the app), then restart it."
echo "   After the server is restarted and listening again at $BASE_URL, press ENTER to continue."
read -rp "Press ENTER when the app has been restarted and is ready... " _

echo
echo "5) Verifying created records exist after restart by unique names..."

# Verify inventory by unique name
inv_get_res=$(curl -s -w "\n%{http_code}" -X GET -H "Accept: application/json" -H "$AUTH_HEADER" "$BASE_URL/api/inventory")
inv_get_status=$(echo "$inv_get_res" | tail -n1)
inv_get_body=$(echo "$inv_get_res" | sed '$d')

if [ "$inv_get_status" != "200" ]; then
  echo "Inventory GET failed (HTTP $inv_get_status). Response:"
  echo "$inv_get_body"
  exit 1
fi

if [ "$HAS_JQ" = true ]; then
  found_item_count=$(echo "$inv_get_body" | jq -r --arg name "$ITEM_BASE_NAME" '[.[] | select(.name == $name)] | length')
else
  found_item_count=$(echo "$inv_get_body" | grep -oF "$ITEM_BASE_NAME" | wc -l)
fi

if [ "$found_item_count" -ge 1 ]; then
  echo "Inventory item with name '$ITEM_BASE_NAME' found after restart. OK."
else
  echo "Inventory item with name '$ITEM_BASE_NAME' NOT found after restart. Response snippet:"
  echo "$inv_get_body" | sed -n '1,200p'
  exit 1
fi

# Verify order by customer name or contained item name
order_search_ok=false

orders_list_res=$(curl -s -w "\n%{http_code}" -X GET -H "Accept: application/json" -H "$AUTH_HEADER" "$BASE_URL/api/order")
orders_list_status=$(echo "$orders_list_res" | tail -n1)
orders_list_body=$(echo "$orders_list_res" | sed '$d')

if [ "$orders_list_status" = "200" ]; then
  if [ "$HAS_JQ" = true ]; then
    order_found_count=$(echo "$orders_list_body" | jq -r --arg cust "$ORDER_CUSTOMER" --arg itemName "$ORDER_ITEM_NAME" '[.[] | select(.customerName == $cust or (.items[]? | select(.name == $itemName) ))] | length')
    if [ "$order_found_count" -ge 1 ]; then
      order_search_ok=true
      echo "Order with customer '$ORDER_CUSTOMER' or item '$ORDER_ITEM_NAME' found in orders list. OK."
    fi
  else
    if echo "$orders_list_body" | grep -qF "$ORDER_CUSTOMER" || echo "$orders_list_body" | grep -qF "$ORDER_ITEM_NAME"; then
      order_search_ok=true
      echo "Order with customer '$ORDER_CUSTOMER' or item '$ORDER_ITEM_NAME' found in orders list. OK."
    fi
  fi
fi

if [ "$order_search_ok" = false ] && [ ${#CREATED_ORDER_IDS[@]} -gt 0 ]; then
  for oid in "${CREATED_ORDER_IDS[@]}"; do
    order_get_res=$(curl -s -w "\n%{http_code}" -X GET -H "Accept: application/json" -H "$AUTH_HEADER" "$BASE_URL/api/order/$oid")
    order_get_status=$(echo "$order_get_res" | tail -n1)
    order_get_body=$(echo "$order_get_res" | sed '$d')
    if [ "$order_get_status" = "200" ]; then
      if [ "$HAS_JQ" = true ]; then
        if echo "$order_get_body" | jq -e --arg itemName "$ORDER_ITEM_NAME" '(.items[]? | select(.name == $itemName))' >/dev/null 2>&1; then
          order_search_ok=true
          echo "Order $oid contains item '$ORDER_ITEM_NAME'. OK."
          break
        fi
      else
        if echo "$order_get_body" | grep -qF "$ORDER_ITEM_NAME"; then
          order_search_ok=true
          echo "Order $oid contains item '$ORDER_ITEM_NAME'. OK."
          break
        fi
      fi
    fi
  done
fi

if [ "$order_search_ok" = false ]; then
  echo "Order with customer '$ORDER_CUSTOMER' or item '$ORDER_ITEM_NAME' NOT found after restart. Response snippets:"
  echo "Orders list (first 200 chars):"
  echo "$orders_list_body" | sed -n '1,200p'
  exit 1
fi

echo
echo "Verification successful! Resources found after restart."
echo "Created item name: $ITEM_BASE_NAME"
if [ ${#CREATED_ITEM_IDS[@]} -gt 0 ]; then
  echo "Numeric item IDs captured: ${CREATED_ITEM_IDS[*]}"
fi
echo "Created order customer: $ORDER_CUSTOMER; order item name: $ORDER_ITEM_NAME"
if [ ${#CREATED_ORDER_IDS[@]} -gt 0 ]; then
  echo "Numeric order IDs captured: ${CREATED_ORDER_IDS[*]}"
fi
echo

# Ask about cleanup
while true; do
  read -rp "Would you like to delete the created test resources now? (y/N): " do_cleanup
  case $do_cleanup in
    [Yy]* ) CLEANUP=true; break;;
    [Nn]* | "" ) CLEANUP=false; break;;
    * ) echo "Please answer y or n.";;
  esac
done

if [ "$CLEANUP" = true ]; then
  echo
  echo "Attempting cleanup..."

  # Delete orders by known IDs first
  if [ ${#CREATED_ORDER_IDS[@]} -gt 0 ]; then
    for oid in "${CREATED_ORDER_IDS[@]}"; do
      echo "Deleting order id $oid..."
      del_res=$(curl -s -w "\n%{http_code}" -X DELETE -H "$AUTH_HEADER" "$BASE_URL/api/order/$oid")
      del_status=$(echo "$del_res" | tail -n1)
      if [ "$del_status" = "204" ] || [ "$del_status" = "200" ]; then
        echo "Deleted order $oid (HTTP $del_status)."
      else
        echo "Failed to delete order $oid (HTTP $del_status). Response:"
        echo "$del_res"
      fi
    done
  fi

  # If no numeric order IDs or to clean any remaining matching orders, search and delete by match
  # This finds order IDs in the orders list that match the customer name or item name
  echo "Searching for any orders matching unique markers to delete..."
  orders_list_res=$(curl -s -w "\n%{http_code}" -X GET -H "Accept: application/json" -H "$AUTH_HEADER" "$BASE_URL/api/order")
  orders_list_status=$(echo "$orders_list_res" | tail -n1)
  orders_list_body=$(echo "$orders_list_res" | sed '$d')

  if [ "$orders_list_status" = "200" ]; then
    if [ "$HAS_JQ" = true ]; then
      to_delete_ids=$(echo "$orders_list_body" | jq -r --arg cust "$ORDER_CUSTOMER" --arg itemName "$ORDER_ITEM_NAME" '.[] | select(.customerName == $cust or (.items[]? | select(.name == $itemName))) | .orderId' )
    else
      # crude extraction: get orderId for orders containing the unique strings
      to_delete_ids=$(echo "$orders_list_body" | awk -v cust="$ORDER_CUSTOMER" -v item="$ORDER_ITEM_NAME" 'BEGIN{RS="}";ORS="\n"} /'"$ORDER_ITEM_NAME"'/ || /'"$ORDER_CUSTOMER"'/ { if (match($0, /"OrderId"[ \t]*:[ \t]*([0-9]+)/, a)) print a[1] }' )
    fi

    if [ -n "$to_delete_ids" ]; then
      for oid in $to_delete_ids; do
        # avoid double-delete if already deleted above
        if printf '%s\n' "${CREATED_ORDER_IDS[@]}" | grep -qx "$oid"; then
          continue
        fi
        echo "Deleting order id $oid (found by search)..."
        del_res=$(curl -s -w "\n%{http_code}" -X DELETE -H "$AUTH_HEADER" "$BASE_URL/api/order/$oid")
        del_status=$(echo "$del_res" | tail -n1)
        if [ "$del_status" = "204" ] || [ "$del_status" = "200" ]; then
          echo "Deleted order $oid (HTTP $del_status)."
        else
          echo "Failed to delete order $oid (HTTP $del_status). Response:"
          echo "$del_res"
        fi
      done
    else
      echo "No matching orders found for deletion by name."
    fi
  else
    echo "Could not list orders to search for deletions (HTTP $orders_list_status)."
  fi

  # Delete inventory items by known IDs first
  if [ ${#CREATED_ITEM_IDS[@]} -gt 0 ]; then
    for iid in "${CREATED_ITEM_IDS[@]}"; do
      echo "Deleting inventory id $iid..."
      del_res=$(curl -s -w "\n%{http_code}" -X DELETE -H "$AUTH_HEADER" "$BASE_URL/api/inventory/$iid")
      del_status=$(echo "$del_res" | tail -n1)
      if [ "$del_status" = "204" ] || [ "$del_status" = "200" ]; then
        echo "Deleted inventory $iid (HTTP $del_status)."
      else
        echo "Failed to delete inventory $iid (HTTP $del_status). Response:"
        echo "$del_res"
      fi
    done
  fi

  # Also search inventory list for items matching the unique name and delete them
  echo "Searching inventory list for items matching unique name to delete..."
  inv_list_res=$(curl -s -w "\n%{http_code}" -X GET -H "Accept: application/json" -H "$AUTH_HEADER" "$BASE_URL/api/inventory")
  inv_list_status=$(echo "$inv_list_res" | tail -n1)
  inv_list_body=$(echo "$inv_list_res" | sed '$d')

  if [ "$inv_list_status" = "200" ]; then
    if [ "$HAS_JQ" = true ]; then
      inv_delete_ids=$(echo "$inv_list_body" | jq -r --arg name "$ITEM_BASE_NAME" '.[] | select(.name == $name) | .itemId' )
    else
      inv_delete_ids=$(echo "$inv_list_body" | grep -oP '"ItemId"\s*:\s*\K[0-9]+' )
      # filter crude list for name match
      # fallback: iterate and check entries containing the name
      inv_delete_ids=""
      echo "$inv_list_body" | awk -v name="$ITEM_BASE_NAME" 'BEGIN{RS="}";ORS="\n"} index($0,name){ if (match($0, /"ItemId"[ \t]*:[ \t]*([0-9]+)/, a)) print a[1] }' > /tmp/_inv_ids.$$ || true
      if [ -f /tmp/_inv_ids.$$ ]; then
        inv_delete_ids=$(cat /tmp/_inv_ids.$$ || true)
        rm -f /tmp/_inv_ids.$$ || true
      fi
    fi

    if [ -n "$inv_delete_ids" ]; then
      for iid in $inv_delete_ids; do
        # avoid double-delete
        if printf '%s\n' "${CREATED_ITEM_IDS[@]}" | grep -qx "$iid"; then
          continue
        fi
        echo "Deleting inventory id $iid (found by search)..."
        del_res=$(curl -s -w "\n%{http_code}" -X DELETE -H "$AUTH_HEADER" "$BASE_URL/api/inventory/$iid")
        del_status=$(echo "$del_res" | tail -n1)
        if [ "$del_status" = "204" ] || [ "$del_status" = "200" ]; then
          echo "Deleted inventory $iid (HTTP $del_status)."
        else
          echo "Failed to delete inventory $iid (HTTP $del_status). Response:"
          echo "$del_res"
        fi
      done
    else
      echo "No matching inventory items found for deletion by name."
    fi
  else
    echo "Could not list inventory to search for deletions (HTTP $inv_list_status)."
  fi

  echo
  echo "Cleanup complete (best-effort). If any deletes failed, check API endpoints and permissions."
else
  echo "Skipping cleanup as requested."
fi

echo
echo "Script finished."

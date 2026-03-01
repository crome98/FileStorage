#!/bin/bash
# Stress test script for large file downloads
# Usage: ./stress-test.sh <concurrent_users> <url>

CONCURRENT=${1:-5}
URL=${2:-"http://localhost:5155/files/pycharm-community-2024.3.1.1-aarch64.dmg"}
RESULTS_FILE="stress-test-results.csv"

echo "Starting stress test: $CONCURRENT concurrent downloads"
echo "URL: $URL"
echo ""

# Initialize results file
echo "user,http_code,time_connect,time_starttransfer,time_total,speed_download,size_download" > "$RESULTS_FILE"

# Function to run single download and capture metrics
download_file() {
    local user_id=$1
    local url=$2

    curl -s -o /dev/null -w "%{http_code},%{time_connect},%{time_starttransfer},%{time_total},%{speed_download},%{size_download}" "$url"
}

export -f download_file

START_TIME=$(date +%s.%N)

# Run concurrent downloads
for i in $(seq 1 $CONCURRENT); do
    (
        result=$(download_file $i "$URL")
        echo "$i,$result" >> "$RESULTS_FILE"
    ) &
done

# Wait for all downloads to complete
wait

END_TIME=$(date +%s.%N)
TOTAL_TIME=$(echo "$END_TIME - $START_TIME" | bc)

echo ""
echo "=== Stress Test Complete ==="
echo "Total test time: ${TOTAL_TIME}s"
echo ""

# Calculate statistics using sort for percentile (macOS compatible)
echo "=== Results Summary ==="
awk -F',' -v total_test_time="$TOTAL_TIME" 'NR>1 {
    count++
    if ($2 == 200) success++
    else errors++
    total_connect += $3
    total_starttransfer += $4
    total_time += $5
    total_speed += $6
    total_bytes += $7

    if ($5 > max_time || NR == 2) max_time = $5
    if ($5 < min_time || NR == 2) min_time = $5
}
END {
    if (count > 0) {
        avg_connect = total_connect / count
        avg_starttransfer = total_starttransfer / count
        avg_time = total_time / count
        avg_speed = total_speed / count

        throughput = count / (total_test_time + 0.001)
        error_rate = (errors / count) * 100

        printf "| %-25s | %-15s |\n", "Metric", "Value"
        printf "| %-25s | %-15s |\n", "-------------------------", "---------------"
        printf "| %-25s | %-15d |\n", "Total Requests", count
        printf "| %-25s | %-15d |\n", "Successful (200)", success
        printf "| %-25s | %-15d |\n", "Errors", errors
        printf "| %-25s | %-13.2f%% |\n", "Error Rate", error_rate
        printf "| %-25s | %-13.3fs |\n", "Avg Connect Time", avg_connect
        printf "| %-25s | %-13.3fs |\n", "Avg Time to First Byte", avg_starttransfer
        printf "| %-25s | %-13.2fs |\n", "Avg Response Time", avg_time
        printf "| %-25s | %-13.2fs |\n", "Min Response Time", min_time
        printf "| %-25s | %-13.2fs |\n", "Max Response Time", max_time
        printf "| %-25s | %-11.2f MB/s |\n", "Avg Download Speed", avg_speed / 1048576
        printf "| %-25s | %-13.2f |\n", "Throughput (req/s)", throughput
        printf "| %-25s | %-11.2f MB |\n", "Total Data Transferred", (total_bytes / 1048576)
    }
}' "$RESULTS_FILE"

# Calculate 95th percentile using sort (macOS compatible)
P95=$(awk -F',' 'NR>1 {print $5}' "$RESULTS_FILE" | sort -n | awk -v count="$CONCURRENT" 'BEGIN {idx = int(count * 0.95); if (idx < 1) idx = 1} NR == idx {print $1}')
echo "| 95th Percentile           | ${P95:-N/A}s          |"

echo ""
echo "Detailed results saved to: $RESULTS_FILE"

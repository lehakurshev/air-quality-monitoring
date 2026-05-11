import http from 'k6/http'
import { check, sleep } from 'k6'

// =====================================
// Config
// =====================================
const BASE_URL = 'https://aq.ural-net.ru'
const SENSOR_COMMUNITY_BASE =
  'https://data.sensor.community/airrohr/v1/sensor'

const SENSOR_IDS = [
  98802,
  42726,
  96034,
]

// =====================================
// Helpers
// =====================================
function randomString(length) {
  const chars =
    'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'

  let result = ''

  for (let i = 0; i < length; i++) {
    result += chars[(Math.random() * chars.length) | 0]
  }

  return result
}

function extractValue(values, type) {
  const item = values.find((v) => v.value_type === type)

  if (!item) return null

  return Number(item.value)
}

// =====================================
// Sensor registration
// =====================================
const sensors = SENSOR_IDS.map((sensorId) => {
  const email = `sensor_${sensorId}_${randomString(8)}@test.local`
  const password = randomString(20)

  const registerRes = http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({
      email,
      password,
    }),
    {
      headers: {
        'Content-Type': 'application/json',
      },
      responseType: 'text',
    }
  )

  check(registerRes, {
    [`register ${sensorId}`]: (r) => r.status === 200,
  })

  if (registerRes.status !== 200) {
    throw new Error(
      `Sensor ${sensorId}: registration failed (${registerRes.status})`
    )
  }

  return {
    sensorId,
    apiToken: registerRes.json().apiToken,
  }
})

// =====================================
// k6 options
// =====================================
export const options = {
  vus: 1,
  iterations: 1,
}

// =====================================
// Main daemon loop
// =====================================
export default function () {
  while (true) {
    for (const sensor of sensors) {
      try {
        // =====================================
        // Get access token
        // =====================================
        const tokenRes = http.post(
          `${BASE_URL}/api/auth/token`,
          JSON.stringify({
            apiToken: sensor.apiToken,
          }),
          {
            headers: {
              'Content-Type': 'application/json',
            },
            responseType: 'text',
          }
        )

        check(tokenRes, {
          [`token ${sensor.sensorId}`]: (r) => r.status === 200,
        })

        if (tokenRes.status !== 200) {
          console.error(
            `Sensor ${sensor.sensorId}: token request failed`
          )

          continue
        }

        const accessToken = tokenRes.json().accessToken

        // =====================================
        // Read Sensor.Community data
        // =====================================
        const sensorRes = http.get(
          `${SENSOR_COMMUNITY_BASE}/${sensor.sensorId}/`,
          {
            responseType: 'text',
          }
        )

        check(sensorRes, {
          [`sensor ${sensor.sensorId} fetch`]:
            (r) => r.status === 200,
        })

        if (sensorRes.status !== 200) {
          console.error(
            `Sensor ${sensor.sensorId}: fetch failed (${sensorRes.status})`
          )

          continue
        }

        const data = sensorRes.json()

        if (!Array.isArray(data) || data.length === 0) {
          console.error(
            `Sensor ${sensor.sensorId}: empty response`
          )

          continue
        }

        // latest measurement
        const latest = data[0]

        const pm10 = extractValue(
          latest.sensordatavalues,
          'P1'
        )

        const pm25 = extractValue(
          latest.sensordatavalues,
          'P2'
        )

        if (pm10 == null || pm25 == null) {
          console.error(
            `Sensor ${sensor.sensorId}: missing P1/P2`
          )

          continue
        }

        // =====================================
        // Send to AQ backend
        // =====================================
        const payload = JSON.stringify({
          pm25,
          pm10,
          latitude: Number(latest.location.latitude),
          longitude: Number(latest.location.longitude),
        })

        const measurementRes = http.post(
          `${BASE_URL}/api/measurement`,
          payload,
          {
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${accessToken}`,
            },
            responseType: 'none',
          }
        )

        check(measurementRes, {
          [`measurement ${sensor.sensorId}`]:
            (r) => r.status === 200,
        })

        console.log(
          `Sensor ${sensor.sensorId}: PM2.5=${pm25}, PM10=${pm10}`
        )
      } catch (e) {
        console.error(
          `Sensor ${sensor.sensorId}: ${String(e)}`
        )
      }
    }

    // wait 5 minutes before next polling cycle
    sleep(300)
  }
}
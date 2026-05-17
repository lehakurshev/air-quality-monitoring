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
  // 🔥 ВСЁ создаём внутри default function (важно для k6)

  const sensors = []

  // =====================================
  // Sensor registration (SAFE now)
  // =====================================
  for (const sensorId of SENSOR_IDS) {
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
      }
    )

    const ok = check(registerRes, {
      [`register ${sensorId}`]: (r) => r.status === 200,
    })

    if (!ok || registerRes.status !== 200) {
      console.error(
        `Sensor ${sensorId}: registration failed (${registerRes.status})`
      )
      continue
    }

    let apiToken = null

    try {
      apiToken = registerRes.json().apiToken
    } catch (e) {
      console.error(`Sensor ${sensorId}: invalid register response`)
      continue
    }

    sensors.push({
      sensorId,
      apiToken,
    })
  }

  // если ничего не зарегистрировалось — стоп
  if (sensors.length === 0) {
    console.error('No sensors registered, exiting iteration')
    return
  }

  // =====================================
  // Infinite daemon loop
  // =====================================
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

        let accessToken = null

        try {
          accessToken = tokenRes.json().accessToken
        } catch (e) {
          console.error(
            `Sensor ${sensor.sensorId}: invalid token response`
          )
          continue
        }

        // =====================================
        // Sensor.Community fetch
        // =====================================
        const sensorRes = http.get(
          `${SENSOR_COMMUNITY_BASE}/${sensor.sensorId}/`
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
        // Send to backend
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
          }
        )

        logRequest('POST', measurementUrl, measurementRes)

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

    // delay between cycles
    sleep(300)
  }
}
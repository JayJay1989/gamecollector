package com.gamecollector.app

import android.annotation.SuppressLint
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean

@SuppressLint("UnsafeOptInUsageError")
@Composable
internal fun BarcodeCamera(
    onBarcode: (String) -> Unit,
    onError: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val currentOnBarcode = rememberUpdatedState(onBarcode)
    val currentOnError = rememberUpdatedState(onError)
    val mainExecutor = remember(context) { ContextCompat.getMainExecutor(context) }
    val analysisExecutor = remember { Executors.newSingleThreadExecutor() }
    val previewView = remember {
        PreviewView(context).apply {
            implementationMode = PreviewView.ImplementationMode.COMPATIBLE
            scaleType = PreviewView.ScaleType.FILL_CENTER
        }
    }
    val scanner = remember {
        val options = BarcodeScannerOptions.Builder()
            .setBarcodeFormats(
                Barcode.FORMAT_EAN_8,
                Barcode.FORMAT_EAN_13,
                Barcode.FORMAT_UPC_A,
                Barcode.FORMAT_UPC_E,
                Barcode.FORMAT_ITF,
                Barcode.FORMAT_CODE_128,
            )
            .build()
        BarcodeScanning.getClient(options)
    }

    DisposableEffect(lifecycleOwner, previewView, scanner) {
        val active = AtomicBoolean(true)
        val analyzing = AtomicBoolean(false)
        val delivered = AtomicBoolean(false)
        val cameraProviderFuture = ProcessCameraProvider.getInstance(context)
        val preview = Preview.Builder().build().also { it.surfaceProvider = previewView.surfaceProvider }
        val analysis = ImageAnalysis.Builder()
            .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
            .build()

        analysis.setAnalyzer(analysisExecutor) { imageProxy ->
            if (delivered.get() || !analyzing.compareAndSet(false, true)) {
                imageProxy.close()
                return@setAnalyzer
            }
            val mediaImage = imageProxy.image
            if (mediaImage == null) {
                analyzing.set(false)
                imageProxy.close()
                return@setAnalyzer
            }

            val input = InputImage.fromMediaImage(mediaImage, imageProxy.imageInfo.rotationDegrees)
            scanner.process(input)
                .addOnSuccessListener { barcodes ->
                    val value = barcodes.firstNotNullOfOrNull { normalizeBarcode(it.rawValue.orEmpty()) }
                    if (value != null && active.get() && delivered.compareAndSet(false, true)) {
                        mainExecutor.execute { if (active.get()) currentOnBarcode.value(value) }
                    }
                }
                .addOnFailureListener {
                    if (active.get()) mainExecutor.execute {
                        if (active.get()) currentOnError.value("The barcode reader could not process the camera image.")
                    }
                }
                .addOnCompleteListener {
                    analyzing.set(false)
                    imageProxy.close()
                }
        }

        cameraProviderFuture.addListener(
            {
                if (!active.get()) return@addListener
                runCatching {
                    cameraProviderFuture.get().apply {
                        unbindAll()
                        bindToLifecycle(lifecycleOwner, CameraSelector.DEFAULT_BACK_CAMERA, preview, analysis)
                    }
                }.onFailure {
                    currentOnError.value("The camera could not be started on this device.")
                }
            },
            mainExecutor,
        )

        onDispose {
            active.set(false)
            analysis.clearAnalyzer()
            if (cameraProviderFuture.isDone) runCatching { cameraProviderFuture.get().unbindAll() }
            scanner.close()
            analysisExecutor.shutdown()
        }
    }

    AndroidView(
        factory = { previewView },
        modifier = modifier.semantics { contentDescription = "Camera preview for barcode scanning" },
    )
}

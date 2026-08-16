"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const core_1 = require("@nestjs/core");
const app_module_1 = require("./app.module");
const common_1 = require("@nestjs/common");
const swagger_1 = require("@nestjs/swagger");
require("dotenv/config");
async function bootstrap() {
    const app = await core_1.NestFactory.create(app_module_1.AppModule);
    app.useGlobalPipes(new common_1.ValidationPipe({ whitelist: true, transform: true }));
    const config = new swagger_1.DocumentBuilder()
        .setTitle("PixelRoad API Document")
        .setDescription("GPS를 기반으로 사용자가 실제 랜드마크를 방문하여 탐험하고 수집하는 교육형 탐험 플랫폼")
        .setVersion("1.0")
        .addBearerAuth()
        .build();
    swagger_1.SwaggerModule.setup("docs", app, swagger_1.SwaggerModule.createDocument(app, config));
    await app.listen(process.env.PORT ?? 3000);
    console.log(`PixelRoad 시작: Http://localhost:${process.env.PORT} (Swagger 문서: /docs)`);
}
bootstrap();
//# sourceMappingURL=main.js.map